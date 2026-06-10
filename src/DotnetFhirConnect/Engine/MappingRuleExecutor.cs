using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.Common;
using Microsoft.Extensions.Logging;
using FhirIdentifier = Hl7.Fhir.Model.Identifier;
using FhirResourceReference = Hl7.Fhir.Model.ResourceReference;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Walks an <see cref="EffectiveMapping"/>'s rule list against a
/// <see cref="BindingContext"/>, dispatching on rule kind and
/// pushing values into the FHIR side via the supplied
/// <see cref="IFhirAdapter"/>. Phase 6b lands the
/// <c>reference</c> / <c>slotArchetype</c> kinds; the extension
/// verbs (add/overwrite/remove) are consumed by
/// <see cref="EffectiveMapping.Build"/> at construction time and
/// never reach this dispatcher.
/// </summary>
internal sealed class MappingRuleExecutor
{
    private static readonly Regex s_ofTypeStripper = new Regex(
        @"\.ofType\([^)]+\)",
        RegexOptions.Compiled);

    private static readonly Regex s_unsupportedFhirPathFragment = new Regex(
        @"\.(where|as|extension|select|exists|first|last|tail|skip|take|repeat)\(",
        RegexOptions.Compiled);

    private readonly IFhirAdapter _adapter;
    private readonly TransformDirection _direction;
    private readonly ILogger? _logger;

    public MappingRuleExecutor(IFhirAdapter adapter, TransformDirection direction, ILogger? logger = null)
    {
        _adapter = adapter;
        _direction = direction;
        _logger = logger;
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public void ExecuteAll(BindingContext rootCtx, IReadOnlyList<MappingRule> rules)
    {
        foreach (MappingRule rule in rules)
        {
            Execute(rootCtx, rule);
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public void Execute(BindingContext ctx, MappingRule rule)
    {
        if (IsUnidirectionalAgainstUs(rule))
        {
            return;
        }

        switch (rule.Kind)
        {
            case RuleKind.Reference:
                ExecuteReference(ctx, rule);
                return;

            case RuleKind.WrapperFollowedBy:
                ExecuteWrapperFollowedBy(ctx, rule, rule.FollowedBy!);
                return;

            case RuleKind.Link:
                ExecuteLink(ctx, rule);
                return;

            case RuleKind.Manual:
                ExecuteManual(ctx, rule, rule.Manual!);
                return;

            case RuleKind.SlotArchetypeMarker:
                // A slotArchetype-only rule with no executable bits
                // and no direct openehr/fhir copy semantics
                // (type:NONE wrapper-less) is an informational
                // binding for the merge layer's transitive
                // bound-name set and has no executable effect. Rules
                // with slotArchetype + type:default + openehr/fhir
                // paths are real direct rules — slotArchetype is
                // just an extra hint — and classify as DirectCopy.
                return;

            case RuleKind.DirectCopy:
                if (_direction == TransformDirection.ToFhir)
                {
                    ExecuteDirectToFhir(ctx, rule);
                }
                else
                {
                    ExecuteDirectToOpenEhr(ctx, rule);
                }
                return;

            case RuleKind.Unknown:
            default:
                // Preserve the pre-Phase-4 silent no-op semantics
                // for inputs that fell off the bottom of the
                // historical if-chain. The loud failure path is the
                // load-time AmbiguousPrimarySlots throw in
                // MappingYamlReader, not the execute-time default
                // arm.
                return;
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteWrapperFollowedBy(BindingContext ctx, MappingRule rule, FollowedBy fb)
    {
        // Compute the inner fhir root: combine the wrapper's with.fhir
        // (or fall back to ctx.FhirRoot when unset) so nested rules
        // address fields relative to the wrapper.
        string innerFhirRoot = rule.With.Fhir is null
            ? ctx.FhirRoot
            : ResolveFhirPath(ctx, NormalizeFhirPath(rule.With.Fhir));

        if (rule.With.OpenEhr is null)
        {
            BindingContext flat = ctx.PushFollowedBy(openEhrRoot: null, fhirRoot: innerFhirRoot);
            foreach (MappingRule child in fb.Mappings)
            {
                Execute(flat, child);
            }
            return;
        }

        foreach (object? item in OpenEhrPathResolver.ResolveMany(ctx, rule.With.OpenEhr))
        {
            if (item is null)
            {
                continue;
            }
            BindingContext nested = ctx.PushFollowedBy(item, innerFhirRoot);
            foreach (MappingRule child in fb.Mappings)
            {
                Execute(nested, child);
            }
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteDirectToFhir(BindingContext ctx, MappingRule rule)
    {
        string rawFhir = rule.With.Fhir!;
        // Structural marker rules whose fhir target is `$resource` (the
        // whole resource itself) have no executable copy semantics —
        // they just establish a binding. Note `$fhirRoot` is NOT a
        // marker — it expands to the enclosing binding's root path,
        // which IS a legitimate write target (e.g. participations
        // wrapper-followedBy children that copy values into the
        // performer collection).
        if (string.Equals(rawFhir, "$resource", StringComparison.Ordinal))
        {
            return;
        }
        object? value = OpenEhrPathResolver.Resolve(ctx, rule.With.OpenEhr!);
        if (value is null)
        {
            return;
        }
        object? translated = OpenEhrToFhirTranslator.Translate(value);
        string fhirPath = ResolveFhirPath(ctx, NormalizeFhirPath(rawFhir));
        if (!_adapter.TrySetValue(ctx.Resource, fhirPath, translated, out string? error))
        {
            throw new InvalidOperationException(
                $"FhirConnectEngine: rule '{rule.Name}' failed to assign FHIR path '{fhirPath}' (raw '{rule.With.Fhir}'): {error}");
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteDirectToOpenEhr(BindingContext ctx, MappingRule rule)
    {
        string fhirPath = ResolveFhirPath(ctx, NormalizeFhirPath(rule.With.Fhir!));
        if (!_adapter.TryGetValue(ctx.Resource, fhirPath, out object? fhirValue) || fhirValue is null)
        {
            return;
        }
        object? translated = FhirToOpenEhrTranslator.Translate(fhirValue);
        if (translated is null)
        {
            return;
        }
        (bool ok, string? err) = OpenEhrPathWriter.Write(ctx.Composition, rule.With.OpenEhr!, translated, logger: _logger);
        if (!ok)
        {
            _ = err;
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteReference(BindingContext ctx, MappingRule rule)
    {
        if (_direction != TransformDirection.ToFhir)
        {
            return;
        }

        if (rule.With.Fhir is null)
        {
            return;
        }

        // The outer rule's with.openehr may be `$reference` (the rule
        // is its own reference root — KDS_composition.fallIdentifikationReference).
        // In that case we cannot resolve an openEHR value at the outer
        // level; the nested mappings carry the data. Skip the resolve.
        object? openEhrValue = null;
        if (rule.With.OpenEhr is not null &&
            !rule.With.OpenEhr.StartsWith("$reference", StringComparison.Ordinal))
        {
            openEhrValue = OpenEhrPathResolver.Resolve(ctx, rule.With.OpenEhr);
        }

        // Build the ResourceReference with Reference="<Type>/<id>".
        string resourceType = rule.Reference?.ResourceType ?? "Resource";
        string id = openEhrValue is null ? string.Empty : ExtractReferenceId(openEhrValue);
        FhirResourceReference rr = new FhirResourceReference
        {
            Reference = string.IsNullOrEmpty(id) ? null : $"{resourceType}/{id}",
        };

        // Run any nested mappings against the freshly-built reference.
        IReadOnlyList<MappingRule>? refMappings = rule.Reference?.Mappings;
        if (refMappings is { Count: > 0 })
        {
            BindingContext nested = ctx.PushReference(rr, fhirRoot: "$resource", referenceRoot: openEhrValue);
            foreach (MappingRule child in refMappings)
            {
                Execute(nested, child);
            }
        }

        // If neither the outer resolve nor any nested rule populated
        // anything meaningful, skip the write to avoid emitting an
        // empty placeholder reference.
        if (rr.Reference is null && rr.Identifier is null && rr.Display is null)
        {
            return;
        }

        string fhirPath = ResolveFhirPath(ctx, NormalizeFhirPath(rule.With.Fhir));
        // FHIRconnect `.reference`-strip heuristic (v0.x interpretation):
        // when an outer reference rule targets `<x>.reference`, the
        // executor treats the trailing `.reference` segment as
        // syntactic sugar meaning "write the freshly built
        // ResourceReference into <x>", not "set <x>.reference to a
        // bare reference string". The whole ResourceReference is
        // pushed into the parent field instead of just its Reference
        // property. This shape is triggered by rules of the form
        //   reference: { ... }
        //   with: { fhir: "$resource.<X>.reference", openehr: "$reference" }
        // where the nested mappings populate the ResourceReference's
        // Identifier / Reference fields. The FHIRconnect v1.0.0 spec
        // is ambiguous on this point; alternative interpretations
        // (set only the string, or always write the full RR) exist
        // and may be chosen by other engines. See the discussion in
        // featurerequest.md item 3b for the deferred-against-spec
        // analysis.
        if (fhirPath.EndsWith(".reference", StringComparison.Ordinal))
        {
            fhirPath = fhirPath.Substring(0, fhirPath.Length - ".reference".Length);
        }
        if (!_adapter.TrySetValue(ctx.Resource, fhirPath, rr, out string? error))
        {
            // Tolerate path-shape mismatches (best-effort).
            _ = error;
        }
    }

    private static string ExtractReferenceId(object openEhrValue)
    {
        return openEhrValue switch
        {
            DotnetOpenEhr.Rm.DataTypes.Uri.DvEhrUri uri when !string.IsNullOrEmpty(uri.Value) =>
                ExtractIdFromEhrUri(uri.Value),
            DotnetOpenEhr.Rm.DataStructures.Cluster cluster => cluster.Uid?.Value ?? cluster.ArchetypeNodeId,
            DotnetOpenEhr.Rm.DataStructures.Element element when element.Value is { } v => v.ToString() ?? string.Empty,
            string s => s,
            _ => openEhrValue.ToString() ?? string.Empty,
        };
    }

    private static string ExtractIdFromEhrUri(string uri)
    {
        const string prefix = "ehr:///compositions/";
        return uri.StartsWith(prefix, StringComparison.Ordinal)
            ? uri.Substring(prefix.Length)
            : uri;
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteLink(BindingContext ctx, MappingRule rule)
    {
        if (_direction != TransformDirection.ToFhir)
        {
            return;
        }

        foreach (object? candidate in OpenEhrPathResolver.ResolveMany(ctx, rule.With.OpenEhr ?? "$archetype/links"))
        {
            if (candidate is not Link link)
            {
                continue;
            }
            if (rule.Link is not null &&
                link.Type is not null &&
                !string.Equals(link.Type.Value, rule.Link.Type, StringComparison.Ordinal))
            {
                continue;
            }

            object? translated = OpenEhrToFhirTranslator.Translate(link.Target);
            string fhirPath = ResolveFhirPath(ctx, NormalizeFhirPath(rule.With.Fhir!));
            if (!_adapter.TrySetValue(ctx.Resource, fhirPath, translated, out string? error))
            {
                // Tolerance is scoped to nested-in-reference link
                // rules: the adapter does not model every
                // reference-shaped link target on
                // ResourceReference, so a swallowed failure inside a
                // reference scope is expected v0.x behaviour.
                // Top-level link rules (e.g. the five
                // partOfReference rules in vital_status.v1.yml) hit
                // R4Adapter's partOf / basedOn / encounter /
                // hasMember arms directly and must surface adapter
                // errors loudly.
                if (ctx.InReferenceRecursion)
                {
                    _ = error;
                    continue;
                }
                throw new InvalidOperationException(
                    $"FhirConnectEngine: link rule '{rule.Name}' failed to assign FHIR path '{fhirPath}' (raw '{rule.With.Fhir}'): {error}");
            }
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteManual(BindingContext ctx, MappingRule rule, IReadOnlyList<ManualEntry> entries)
    {
        if (_direction == TransformDirection.ToFhir)
        {
            // If the manual-bearing rule carries its own with.fhir,
            // descend the root one more level so field.Path resolves
            // under (ctx.FhirRoot + rule.with.fhir).
            string root = rule.With.Fhir is null
                ? ctx.FhirRoot
                : CombineRelativeFhirPath(ctx, rule.With.Fhir);

            foreach (ManualEntry entry in entries)
            {
                if (entry.Fhir is null)
                {
                    continue;
                }
                foreach (ManualField field in entry.Fhir)
                {
                    string fhirPath = CombineFhirPath(root, field.Path);
                    if (!_adapter.TrySetValue(ctx.Resource, fhirPath, field.Value, out string? error))
                    {
                        _ = error;
                    }
                }
            }
            return;
        }

        // ToOpenEhr: write manual openEHR-side constants back into the
        // typed Composition graph.
        foreach (ManualEntry entry in entries)
        {
            if (entry.OpenEhr is null)
            {
                continue;
            }
            foreach (ManualField field in entry.OpenEhr)
            {
                DotnetOpenEhr.Rm.DataTypes.Text.DvText boxed =
                    new DotnetOpenEhr.Rm.DataTypes.Text.DvText { Value = field.Value };
                (bool ok, string? err) = OpenEhrPathWriter.Write(ctx.Composition, field.Path, boxed, logger: _logger);
                if (!ok)
                {
                    _ = err;
                }
            }
        }
    }

    private static string CombineFhirPath(string root, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return root;
        }
        if (path.StartsWith('$'))
        {
            return path;
        }
        return root.EndsWith('.') ? root + path : root + "." + path;
    }

    /// <summary>
    /// Resolve a rule's <c>with.fhir</c> path with relative-path
    /// semantics: <c>$fhirRoot</c> / <c>$resource</c> prefixes pass
    /// through (or substitute), but a bare path like <c>"coding"</c>
    /// is combined onto <see cref="BindingContext.FhirRoot"/>.
    /// </summary>
    private static string CombineRelativeFhirPath(BindingContext ctx, string fhirPath)
    {
        string normalized = NormalizeFhirPath(fhirPath);
        if (string.IsNullOrEmpty(normalized))
        {
            return ctx.FhirRoot;
        }
        if (normalized.StartsWith('$'))
        {
            return ResolveFhirPath(ctx, normalized);
        }
        return CombineFhirPath(ctx.FhirRoot, normalized);
    }

    /// <summary>
    /// Resolve a rule's <c>with.fhir</c> path to the absolute path
    /// the <see cref="IFhirAdapter"/> expects: <c>$fhirRoot</c> /
    /// <c>$fhirRoot.X</c> get substituted with the enclosing
    /// <see cref="BindingContext.FhirRoot"/>; bare paths / explicit
    /// <c>$resource</c> paths pass through.
    /// </summary>
    private static string ResolveFhirPath(BindingContext ctx, string fhirPath)
    {
        if (string.IsNullOrEmpty(fhirPath))
        {
            return ctx.FhirRoot;
        }
        if (fhirPath.StartsWith("$fhirRoot", StringComparison.Ordinal))
        {
            string remainder = fhirPath.Substring("$fhirRoot".Length);
            if (string.IsNullOrEmpty(remainder))
            {
                return ctx.FhirRoot;
            }
            return ctx.FhirRoot + remainder;
        }
        return fhirPath;
    }

    /// <summary>
    /// Strip the FHIRPath <c>.ofType(&lt;Type&gt;)</c> segment from
    /// a path expression — the only FHIRPath construct v0.x
    /// recognises in <c>with.fhir</c>. Any other construct
    /// (<c>where(...)</c>, <c>as(...)</c>, etc.) throws so silent
    /// mis-mapping is impossible.
    /// </summary>
    internal static string NormalizeFhirPath(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }
        // Strip ofType first so the unsupported-fragment check is not
        // confused by the legal construct.
        string stripped = s_ofTypeStripper.Replace(raw, string.Empty);
        if (s_unsupportedFhirPathFragment.IsMatch(stripped))
        {
            throw new NotSupportedException(
                $"MappingRuleExecutor: FHIRPath construct in '{raw}' is not supported in v0.x; only '.ofType(<Type>)' is whitelisted.");
        }
        return stripped;
    }

    private bool IsUnidirectionalAgainstUs(MappingRule rule)
    {
        if (rule.Unidirectional is null)
        {
            return false;
        }
        return _direction switch
        {
            TransformDirection.ToFhir => !string.Equals(rule.Unidirectional, "to_fhir", StringComparison.Ordinal),
            TransformDirection.ToOpenEhr => !string.Equals(rule.Unidirectional, "to_openehr", StringComparison.Ordinal),
            _ => false,
        };
    }
}
