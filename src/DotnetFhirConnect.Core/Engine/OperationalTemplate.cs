using System.Collections.Generic;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using SdkOperationalTemplate = DotnetOpenEhr.Templates.OperationalTemplate;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Adapts a SDK <see cref="SdkOperationalTemplate"/> to the engine's
/// <see cref="IOperationalTemplate"/> seam. Element names are resolved
/// from the OPT's terminology: the active component archetype's
/// terminology first, then the root terminology, then <c>null</c>.
/// </summary>
internal sealed class OptTemplateAdapter : IOperationalTemplate
{
    private readonly SdkOperationalTemplate _opt;

    public OptTemplateAdapter(SdkOperationalTemplate opt)
    {
        _opt = opt ?? throw new System.ArgumentNullException(nameof(opt));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Prefers the friendly id stored in
    /// <c>HeaderMetadata["template_id"]</c> (e.g. <c>KDS_Vitalstatus</c>)
    /// over the SDK's <see cref="SdkOperationalTemplate.TemplateId"/>,
    /// which returns the root archetype concept id (e.g. <c>report</c>).
    /// </remarks>
    public string TemplateId =>
        _opt.HeaderMetadata.TryGetValue("template_id", out string? id) && !string.IsNullOrEmpty(id)
            ? id
            : _opt.TemplateId;

    /// <inheritdoc />
    public string? ResolveElementName(string? archetypeId, string atCode)
    {
        if (string.IsNullOrEmpty(atCode))
        {
            return null;
        }

        // Component terminology first (the at-codes for vital_status live
        // under the EVALUATION component, not the COMPOSITION root).
        if (!string.IsNullOrEmpty(archetypeId))
        {
            ArchetypeTerminology? component = FindComponentTerminology(archetypeId);
            if (component is not null)
            {
                string? hit = ResolveFromTerminology(component, atCode);
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        // Fall back to the root terminology.
        return ResolveFromTerminology(_opt.Terminology, atCode);
    }

    /// <summary>
    /// Match a <c>ComponentTerminologies</c> entry by a
    /// <c>ToString()</c>-normalized comparison rather than a keyed lookup:
    /// <see cref="ArchetypeHRID"/> equality is version-format-exact
    /// (<c>.v1</c> ≠ <c>.v1.0.0</c>), so scanning by string is robust to
    /// equivalent-but-differently-formatted version strings. Mirrors the
    /// SDK's own test pattern.
    /// </summary>
    private ArchetypeTerminology? FindComponentTerminology(string archetypeId)
    {
        foreach (KeyValuePair<ArchetypeHRID, ArchetypeTerminology> entry in _opt.ComponentTerminologies)
        {
            if (string.Equals(entry.Key.ToString(), archetypeId, System.StringComparison.Ordinal))
            {
                return entry.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// Resolve <paramref name="atCode"/> within
    /// <paramref name="terminology"/>: pick the original language when it
    /// is present in <c>TermDefinitions</c>, else the first available
    /// language, and return the term's text when found.
    /// </summary>
    private static string? ResolveFromTerminology(ArchetypeTerminology terminology, string atCode)
    {
        if (terminology.TermDefinitions.Count == 0)
        {
            return null;
        }

        string? lang = null;
        if (!string.IsNullOrEmpty(terminology.OriginalLanguage) &&
            terminology.TermDefinitions.ContainsKey(terminology.OriginalLanguage))
        {
            lang = terminology.OriginalLanguage;
        }
        else
        {
            foreach (string key in terminology.TermDefinitions.Keys)
            {
                lang = key;
                break;
            }
        }

        if (lang is null)
        {
            return null;
        }

        if (terminology.TermDefinitions.TryGetValue(lang, out Dictionary<string, ArchetypeTerm>? terms) &&
            terms.TryGetValue(atCode, out ArchetypeTerm? term) &&
            !string.IsNullOrEmpty(term.Text))
        {
            return term.Text;
        }

        return null;
    }
}
