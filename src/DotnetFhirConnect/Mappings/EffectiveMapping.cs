using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// One diagnostic emitted by <see cref="EffectiveMapping.Build"/>
/// when the merge layer makes a non-trivial decision worth surfacing
/// to the caller (e.g., a transitive slot-archetype binding, or a
/// fan-out merge into multiple matches).
/// </summary>
public sealed record MergeWarning(string RuleName, string Reason, MergeWarningKind Kind);

/// <summary>
/// Classifier on a <see cref="MergeWarning"/> so callers can filter
/// without parsing the human-readable reason text.
/// </summary>
public enum MergeWarningKind
{
    /// <summary>
    /// The extension bound to the model because one of its
    /// transitively-referenced <c>slotArchetype</c>s appeared in the
    /// bound-name set, not because the start model name matched.
    /// </summary>
    TransitiveBinding,

    /// <summary>
    /// An <c>extension: add</c> rule's composite key matched more
    /// than one existing rule; the merge fanned out to every match.
    /// </summary>
    MultiMatchAddFanOut,

    /// <summary>
    /// A merge decision that does not fit the more specific kinds.
    /// </summary>
    Other,
}

/// <summary>
/// The result of merging a <see cref="ModelMapping"/> with all the
/// applicable <see cref="ExtensionMapping"/>s in a bundle. The
/// <see cref="Rules"/> list is what the executor walks at run time
/// instead of <see cref="ModelMapping.Mappings"/>.
/// </summary>
public sealed record EffectiveMapping(
    ModelMapping Source,
    IReadOnlyList<MappingRule> Rules,
    IReadOnlyList<MergeWarning> MergeWarnings)
{
    /// <summary>
    /// Merge <paramref name="model"/> with every extension in
    /// <paramref name="extensions"/> whose <c>spec.extends</c>
    /// transitively binds to the model's start name or to a
    /// <c>slotArchetype</c> referenced from a bound rule. Throws
    /// <see cref="FhirConnectFormatException"/> for ambiguous /
    /// missing-target overwrite or remove verbs.
    /// </summary>
    public static EffectiveMapping Build(
        ModelMapping model,
        IEnumerable<ExtensionMapping> extensions,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(extensions);

        List<MappingRule> working = new List<MappingRule>(model.Mappings);
        HashSet<string> bound = new HashSet<string>(StringComparer.Ordinal)
        {
            model.Metadata.Name,
        };
        HashSet<string> applied = new HashSet<string>(StringComparer.Ordinal);
        List<MergeWarning> warnings = [];

        // Seed bound set with slot archetypes already referenced by
        // the model rules so first-pass extension matching catches
        // every legal binding.
        foreach (string slot in CollectSlotArchetypes(working))
        {
            bound.Add(slot);
        }

        List<ExtensionMapping> remaining = extensions.ToList();
        bool progressed;
        do
        {
            progressed = false;
            for (int i = remaining.Count - 1; i >= 0; i--)
            {
                ExtensionMapping ext = remaining[i];
                string? extends = ext.Spec.Extends;
                if (extends is null || !bound.Contains(extends))
                {
                    continue;
                }
                bool wasTransitive = !string.Equals(extends, model.Metadata.Name, StringComparison.Ordinal);
                if (wasTransitive)
                {
                    MergeWarning w = new MergeWarning(
                        RuleName: ext.Metadata.Name,
                        Reason: $"Extension '{ext.Metadata.Name}' bound transitively via slotArchetype '{extends}'.",
                        Kind: MergeWarningKind.TransitiveBinding);
                    warnings.Add(w);
                    logger?.LogInformation(
                        "FhirConnect merge: extension '{ExtensionName}' bound transitively via slotArchetype '{SlotArchetype}'.",
                        ext.Metadata.Name, extends);
                }

                MergeExtension(ext, working, warnings, logger);
                applied.Add(ext.Metadata.Name);
                remaining.RemoveAt(i);
                progressed = true;

                // Re-scan slot archetypes after each merge so the next
                // loop iteration picks up any newly-bound names.
                foreach (string slot in CollectSlotArchetypes(working))
                {
                    bound.Add(slot);
                }
            }
        } while (progressed);

        return new EffectiveMapping(model, working, warnings);
    }

    private static IEnumerable<string> CollectSlotArchetypes(IEnumerable<MappingRule> rules)
    {
        foreach (MappingRule r in rules)
        {
            if (!string.IsNullOrEmpty(r.SlotArchetype))
            {
                yield return r.SlotArchetype;
            }
            if (r.Reference is { Mappings: { } refMappings })
            {
                foreach (string s in CollectSlotArchetypes(refMappings))
                {
                    yield return s;
                }
            }
            if (r.FollowedBy is { Mappings: { } fbMappings })
            {
                foreach (string s in CollectSlotArchetypes(fbMappings))
                {
                    yield return s;
                }
            }
        }
    }

    private static void MergeExtension(
        ExtensionMapping ext,
        List<MappingRule> working,
        List<MergeWarning> warnings,
        ILogger? logger)
    {
        foreach (MappingRule overlay in ext.Mappings)
        {
            ExtensionAction verb = overlay.Extension ?? ExtensionAction.Add;
            (string name, string fhir, string disambig) key = KeyOf(overlay);
            List<int> matches = FindMatches(working, key);

            switch (verb)
            {
                case ExtensionAction.Add when matches.Count == 0:
                    working.Add(overlay);
                    break;

                case ExtensionAction.Add when matches.Count == 1:
                    working[matches[0]] = MergeAdd(working[matches[0]], overlay);
                    break;

                case ExtensionAction.Add:
                    {
                        MergeWarning w = new MergeWarning(
                            RuleName: overlay.Name,
                            Reason: $"Extension '{ext.Metadata.Name}' add rule '{overlay.Name}' matched {matches.Count} rules; fanning out merge to every match.",
                            Kind: MergeWarningKind.MultiMatchAddFanOut);
                        warnings.Add(w);
                        logger?.LogInformation(
                            "FhirConnect merge: '{Ext}'.'{Rule}' add fanout to {Count} matches.",
                            ext.Metadata.Name, overlay.Name, matches.Count);
                        foreach (int idx in matches)
                        {
                            working[idx] = MergeAdd(working[idx], overlay);
                        }
                        break;
                    }

                case ExtensionAction.Overwrite when matches.Count == 1:
                    working[matches[0]] = overlay;
                    break;

                case ExtensionAction.Overwrite when matches.Count == 0:
                    throw new FhirConnectFormatException(
                        $"Extension '{ext.Metadata.Name}' rule '{overlay.Name}' uses extension:overwrite but no matching model rule exists for key (name='{key.name}', fhir='{key.fhir}', disambiguator='{key.disambig}').",
                        ext.Metadata.Name);

                case ExtensionAction.Overwrite:
                    throw new FhirConnectFormatException(
                        $"Extension '{ext.Metadata.Name}' rule '{overlay.Name}' uses extension:overwrite but matches {matches.Count} model rules; the spec must disambiguate further before overwrite can be applied.",
                        ext.Metadata.Name);

                case ExtensionAction.Remove when matches.Count == 1:
                    working.RemoveAt(matches[0]);
                    break;

                case ExtensionAction.Remove when matches.Count == 0:
                    throw new FhirConnectFormatException(
                        $"Extension '{ext.Metadata.Name}' rule '{overlay.Name}' uses extension:remove but no matching model rule exists for key (name='{key.name}', fhir='{key.fhir}', disambiguator='{key.disambig}').",
                        ext.Metadata.Name);

                case ExtensionAction.Remove:
                    throw new FhirConnectFormatException(
                        $"Extension '{ext.Metadata.Name}' rule '{overlay.Name}' uses extension:remove but matches {matches.Count} model rules; the spec must disambiguate further before remove can be applied.",
                        ext.Metadata.Name);
            }
        }
    }

    /// <summary>
    /// Composite key for matching extension rules against the working
    /// list. Distinguishes the five same-named <c>partOfReference</c>
    /// rules in the vital_status model by their <c>with.fhir</c> path
    /// and link.type / slotArchetype disambiguator.
    /// </summary>
    private static (string name, string fhir, string disambig) KeyOf(MappingRule r)
    {
        string disambig = r.Link?.Type ?? r.SlotArchetype ?? string.Empty;
        return (r.Name, r.With.Fhir ?? string.Empty, disambig);
    }

    private static List<int> FindMatches(List<MappingRule> working, (string name, string fhir, string disambig) key)
    {
        List<int> hits = [];
        for (int i = 0; i < working.Count; i++)
        {
            (string name, string fhir, string disambig) k = KeyOf(working[i]);
            if (string.Equals(k.name, key.name, StringComparison.Ordinal) &&
                string.Equals(k.fhir, key.fhir, StringComparison.Ordinal) &&
                string.Equals(k.disambig, key.disambig, StringComparison.Ordinal))
            {
                hits.Add(i);
            }
        }
        return hits;
    }

    private static MappingRule MergeAdd(MappingRule baseRule, MappingRule overlay)
    {
        IReadOnlyList<MappingRule>? mergedFb = null;
        if (baseRule.FollowedBy is null && overlay.FollowedBy is null)
        {
            mergedFb = null;
        }
        else
        {
            List<MappingRule> mergedList = [];
            if (baseRule.FollowedBy is not null) { mergedList.AddRange(baseRule.FollowedBy.Mappings); }
            if (overlay.FollowedBy is not null) { mergedList.AddRange(overlay.FollowedBy.Mappings); }
            mergedFb = mergedList;
        }

        IReadOnlyList<ManualEntry>? mergedManual;
        if (baseRule.Manual is null && overlay.Manual is null)
        {
            mergedManual = null;
        }
        else
        {
            List<ManualEntry> mergedList = [];
            if (baseRule.Manual is not null) { mergedList.AddRange(baseRule.Manual); }
            if (overlay.Manual is not null) { mergedList.AddRange(overlay.Manual); }
            mergedManual = mergedList;
        }

        return baseRule with
        {
            FollowedBy = mergedFb is null ? null : new FollowedBy(mergedFb),
            Manual = mergedManual,
            SlotArchetype = overlay.SlotArchetype ?? baseRule.SlotArchetype,
            Reference = overlay.Reference ?? baseRule.Reference,
        };
    }
}
