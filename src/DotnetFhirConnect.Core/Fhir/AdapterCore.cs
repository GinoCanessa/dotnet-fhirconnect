using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Hl7.Fhir.Model;

namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Release-agnostic adapter core: owns FHIRconnect path
/// normalisation, value coercion against un-aliased
/// <c>Hl7.Fhir.Base</c> types (<see cref="ResourceReference"/>,
/// <see cref="Identifier"/>, <see cref="CodeableConcept"/>,
/// <see cref="Coding"/>, <see cref="Markdown"/>), and dispatch into
/// the per-release <see cref="IObservationShim"/>. The three
/// <c>R4Adapter</c> / <c>R4BAdapter</c> / <c>R5Adapter</c> shells
/// hold only release-specific parse / serialize plumbing plus the
/// shim instance.
/// </summary>
/// <remarks>
/// Adding a new FHIR release means: (1) add a per-release shim
/// implementing <see cref="IObservationShim"/>, (2) add an adapter
/// shell pointing at it, (3) add a switch arm in
/// <see cref="FhirAdapterFactory"/>. The path dispatch arms below
/// are touched once per shape, not three times.
/// </remarks>
internal static class AdapterCore
{
    private static readonly Regex s_nestedCodingPath = new Regex(
        @"^(?<parent>code|category)(?:\[(?<pidx>\d+)\])?\.coding(?:\[(?<idx>\d+)\])?\.(?<field>code|system|display)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Strip the leading <c>$resource.</c> and/or
    /// <c>Observation.</c> prefix from a FHIRconnect rule path so
    /// the dispatch arms see the structural tail
    /// (<c>note.text</c>, <c>value</c>, etc.) regardless of which
    /// prefix the caller passed.
    /// </summary>
    internal static string StripResourcePrefix(string path)
    {
        string p = path;
        if (p.StartsWith("$resource.", StringComparison.Ordinal))
        {
            p = p.Substring("$resource.".Length);
        }
        if (p.StartsWith("Observation.", StringComparison.Ordinal))
        {
            p = p.Substring("Observation.".Length);
        }
        return p;
    }

    /// <summary>
    /// Path-dispatched write. Handles the <see cref="ResourceReference"/>
    /// arm directly (using shared <c>Hl7.Fhir.Base</c> types); for
    /// Observation-shaped resources delegates leaf writes to the
    /// shim.
    /// </summary>
    internal static bool TrySetValue(
        IObservationShim shim,
        string adapterDisplayName,
        object resource,
        string path,
        object? value,
        [NotNullWhen(false)] out string? error)
    {
        // ResourceReference is a valid target for the identifier
        // sub-rule under a `reference` block. The dispatch arm lives
        // here in AdapterCore because ResourceReference / Identifier
        // are un-aliased Hl7.Fhir.Base types shared across releases.
        if (resource is ResourceReference rr)
        {
            string rrp = StripResourcePrefix(path);
            if (string.Equals(rrp, "identifier", StringComparison.Ordinal))
            {
                rr.Identifier = AsIdentifier(value);
                error = null;
                return true;
            }
            error = $"{adapterDisplayName}: unknown ResourceReference path '{path}'.";
            return false;
        }

        if (!shim.IsObservation(resource))
        {
            error = $"{adapterDisplayName}: only Observation is supported in v0.x; got {resource?.GetType().Name ?? "null"}.";
            return false;
        }

        string p = StripResourcePrefix(path);
        try
        {
            switch (p)
            {
                case "code":
                    shim.SetCode(resource, AsCodeableConcept(value));
                    error = null;
                    return true;

                case "category":
                    shim.SetCategory0(resource, AsCodeableConcept(value));
                    error = null;
                    return true;

                case "value":
                    shim.SetValue(resource, AsDataType(value));
                    error = null;
                    return true;

                case "effective":
                    shim.SetEffective(resource, AsDataType(value));
                    error = null;
                    return true;

                case "note.text":
                    shim.SetNote0Text(resource, AsMarkdown(value));
                    error = null;
                    return true;

                case "performer":
                    shim.AddPerformer(resource, AsReference(value));
                    error = null;
                    return true;

                case "partOf":
                    shim.AddPartOf(resource, AsReference(value));
                    error = null;
                    return true;

                case "basedOn":
                    shim.AddBasedOn(resource, AsReference(value));
                    error = null;
                    return true;

                case "hasMember":
                    shim.AddHasMember(resource, AsReference(value));
                    error = null;
                    return true;

                case "encounter":
                    shim.SetEncounter(resource, AsReference(value));
                    error = null;
                    return true;

                case "encounter.identifier":
                    shim.SetEncounterIdentifier(resource, AsIdentifier(value));
                    error = null;
                    return true;

                case "encounter.reference":
                    shim.SetEncounterReference(resource, AsString(value));
                    error = null;
                    return true;

                default:
                    if (TrySetNestedCodingField(shim, resource, p, value))
                    {
                        error = null;
                        return true;
                    }
                    error = $"{adapterDisplayName}: unknown Observation path '{path}' (normalized '{p}').";
                    return false;
            }
        }
        catch (InvalidCastException ex)
        {
            error = $"{adapterDisplayName}: cannot coerce value for path '{path}': {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Path-dispatched read. Mirror of
    /// <see cref="TrySetValue"/>'s read-side: shim-only access for
    /// Observation-shaped resources.
    /// </summary>
    internal static bool TryGetValue(
        IObservationShim shim,
        object resource,
        string path,
        out object? value)
    {
        if (!shim.IsObservation(resource))
        {
            value = null;
            return false;
        }

        string p = StripResourcePrefix(path);
        switch (p)
        {
            case "code":
                value = shim.GetCode(resource);
                return value is not null;

            case "category":
                value = shim.GetCategory0(resource);
                return value is not null;

            case "value":
                value = shim.GetValue(resource);
                return value is not null;

            case "effective":
                value = shim.GetEffective(resource);
                return value is not null;

            case "note.text":
                Markdown? md = shim.GetNote0Text(resource);
                if (md?.Value is not null)
                {
                    value = md.Value;
                    return true;
                }
                value = null;
                return false;

            case "performer":
                value = shim.GetPerformer0(resource);
                return value is not null;

            case "partOf":
                value = shim.GetPartOf0(resource);
                return value is not null;

            case "basedOn":
                value = shim.GetBasedOn0(resource);
                return value is not null;

            case "hasMember":
                value = shim.GetHasMember0(resource);
                return value is not null;

            case "encounter":
                value = shim.GetEncounter(resource);
                return value is not null;

            case "code.coding[0].code":
                if (shim.GetCode(resource) is { Coding.Count: > 0 } cc1)
                {
                    value = cc1.Coding[0].Code;
                    return value is not null;
                }
                value = null;
                return false;

            case "code.coding[0].system":
                if (shim.GetCode(resource) is { Coding.Count: > 0 } cc2)
                {
                    value = cc2.Coding[0].System;
                    return value is not null;
                }
                value = null;
                return false;

            case "category[0].coding[0].code":
                if (shim.GetCategory0(resource) is { Coding.Count: > 0 } cat1)
                {
                    value = cat1.Coding[0].Code;
                    return value is not null;
                }
                value = null;
                return false;

            case "category[0].coding[0].system":
                if (shim.GetCategory0(resource) is { Coding.Count: > 0 } cat2)
                {
                    value = cat2.Coding[0].System;
                    return value is not null;
                }
                value = null;
                return false;

            default:
                value = null;
                return false;
        }
    }

    private static bool TrySetNestedCodingField(
        IObservationShim shim,
        object resource,
        string normalisedPath,
        object? value)
    {
        Match m = s_nestedCodingPath.Match(normalisedPath);
        if (!m.Success)
        {
            return false;
        }
        int codingIdx = m.Groups["idx"].Success
            ? int.Parse(m.Groups["idx"].Value, CultureInfo.InvariantCulture)
            : 0;
        // Note: parentIdx is captured but unused for v0.x — only
        // category[0] is exercised by the bundle. Keeping the
        // capture for future expansion.
        _ = m.Groups["pidx"];

        bool isCode = string.Equals(m.Groups["parent"].Value, "code", StringComparison.Ordinal);

        // Walk to (or create) the target CodeableConcept via the
        // shim, then mutate its Coding[codingIdx]. All mutation
        // happens on shared Hl7.Fhir.Base.CodeableConcept / Coding
        // POCOs, so no further shim hop is needed beyond
        // ensure-and-fetch.
        CodeableConcept? cc;
        if (isCode)
        {
            cc = shim.GetCode(resource);
            if (cc is null)
            {
                cc = new CodeableConcept();
                shim.SetCode(resource, cc);
            }
        }
        else
        {
            cc = shim.GetCategory0(resource);
            if (cc is null)
            {
                cc = new CodeableConcept();
                shim.SetCategory0(resource, cc);
            }
        }

        while (cc.Coding.Count <= codingIdx)
        {
            cc.Coding.Add(new Coding());
        }
        string? strValue = AsString(value);
        switch (m.Groups["field"].Value)
        {
            case "code":
                cc.Coding[codingIdx].Code = strValue;
                return true;
            case "system":
                cc.Coding[codingIdx].System = strValue;
                return true;
            case "display":
                cc.Coding[codingIdx].Display = strValue;
                return true;
        }
        return false;
    }

    internal static CodeableConcept AsCodeableConcept(object? value)
    {
        return value switch
        {
            null => new CodeableConcept(),
            CodeableConcept cc => cc,
            string s => new CodeableConcept(system: null, code: s),
            Coding c => new CodeableConcept { Coding = [c] },
            _ => throw new InvalidCastException(
                $"Cannot coerce {value.GetType().Name} to CodeableConcept."),
        };
    }

    internal static DataType AsDataType(object? value)
    {
        return value switch
        {
            null => throw new InvalidCastException("Null cannot be assigned to a choice element."),
            DataType d => d,
            string s => new FhirString(s),
            DateTimeOffset dto => new FhirDateTime(dto),
            _ => throw new InvalidCastException(
                $"Cannot coerce {value.GetType().Name} to a FHIR DataType."),
        };
    }

    internal static Markdown AsMarkdown(object? value)
    {
        return value switch
        {
            null => new Markdown(),
            Markdown md => md,
            string s => new Markdown(s),
            _ => throw new InvalidCastException(
                $"Cannot coerce {value.GetType().Name} to Markdown."),
        };
    }

    internal static ResourceReference AsReference(object? value)
    {
        return value switch
        {
            null => new ResourceReference(),
            ResourceReference r => r,
            string s => new ResourceReference { Display = s },
            _ => throw new InvalidCastException(
                $"Cannot coerce {value.GetType().Name} to ResourceReference."),
        };
    }

    /// <summary>
    /// Coerce a value to an <see cref="Identifier"/>. Now that the
    /// translator handles <c>Cluster</c> natively at the openEHR
    /// edge, an incoming Cluster shape here is a contract violation,
    /// not a fallback case — throw rather than silently
    /// <c>ToString()</c>.
    /// </summary>
    internal static Identifier AsIdentifier(object? value)
    {
        return value switch
        {
            null => new Identifier(),
            Identifier id => id,
            string s => new Identifier { Value = s },
            DotnetOpenEhr.Rm.DataStructures.Cluster =>
                throw new InvalidCastException(
                    "AdapterCore.AsIdentifier: a Cluster reached the adapter — the openEHR-side translator should have extracted an Identifier before the value crossed the seam. Check OpenEhrToFhirTranslator coverage."),
            _ => new Identifier { Value = value.ToString() },
        };
    }

    internal static string? AsString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            _ => value.ToString(),
        };
    }
}
