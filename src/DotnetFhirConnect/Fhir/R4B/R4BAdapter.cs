extern alias coreR4B;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Annotation = coreR4B::Hl7.Fhir.Model.Annotation;
using Observation = coreR4B::Hl7.Fhir.Model.Observation;

namespace DotnetFhirConnect.Fhir.R4B;

/// <summary>
/// HL7 FHIR R4 implementation of <see cref="IFhirAdapter"/>, built
/// on top of <c>Hl7.Fhir.R4B</c>. The path resolver handles only the
/// property graph the v0.x vital_status walking-skeleton mapping
/// touches on <see cref="Observation"/> — extension is straightforward
/// (add a switch arm); routing every FHIR path through
/// <c>FhirPathCompiler</c> is explicitly out of scope for v0.x.
/// </summary>
public sealed class R4BAdapter : IFhirAdapter
{
    private static readonly Hl7.Fhir.Introspection.ModelInspector s_inspector =
        coreR4B::Hl7.Fhir.Model.ModelInfo.ModelInspector;
    private static readonly BaseFhirJsonDeserializer s_deserializer =
        new BaseFhirJsonDeserializer(s_inspector);
    private static readonly BaseFhirJsonSerializer s_serializer =
        new BaseFhirJsonSerializer(s_inspector);

    /// <inheritdoc/>
    public FhirRelease Release => FhirRelease.R4B;

    /// <inheritdoc/>
    [RequiresUnreferencedCode(
        "Hl7.Fhir.R4B parsers traverse the typed POCO graph and are not currently AOT-clean. "
        + "Library is not AOT-publishable in v0.x.")]
    public object ParseResource(ReadOnlySpan<char> json)
    {
        Utf8JsonReader reader = new Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(json.ToString()));
        return s_deserializer.DeserializeResource(ref reader);
    }

    /// <inheritdoc/>
    [RequiresUnreferencedCode("See ParseResource.")]
    public string SerializeResource(object resource)
    {
        if (resource is not Resource r)
        {
            throw new ArgumentException(
                $"R4BAdapter.SerializeResource: expected Hl7.Fhir.R4B.Model.Resource, got {resource?.GetType().FullName ?? "null"}.",
                nameof(resource));
        }
        return s_serializer.SerializeToString(r);
    }

    /// <inheritdoc/>
    public object CreateResource(string typeName)
    {
        return typeName switch
        {
            "Observation" => new Observation(),
            _ => throw new ArgumentException(
                $"R4BAdapter.CreateResource: type '{typeName}' not registered in v0.x. " +
                "Add a switch arm in R4BAdapter or wait for Phase 6b to broaden coverage.",
                nameof(typeName)),
        };
    }

    /// <inheritdoc/>
    public bool TrySetValue(
        object resource,
        string path,
        object? value,
        [NotNullWhen(false)] out string? error)
    {
        // ResourceReference is a valid target for the
        // identifier sub-rule under a `reference` block.
        if (resource is ResourceReference rr)
        {
            string rrp = StripResourcePrefix(path);
            if (string.Equals(rrp, "identifier", StringComparison.Ordinal))
            {
                rr.Identifier = AsIdentifier(value);
                error = null;
                return true;
            }
            error = $"R4BAdapter: unknown ResourceReference path '{path}'.";
            return false;
        }

        if (resource is not Observation obs)
        {
            error = $"R4BAdapter: only Observation is supported in v0.x; got {resource?.GetType().Name ?? "null"}.";
            return false;
        }

        string p = StripResourcePrefix(path);
        try
        {
            switch (p)
            {
                case "code":
                    obs.Code = AsCodeableConcept(value);
                    error = null;
                    return true;

                case "category":
                    CodeableConcept catCC = AsCodeableConcept(value);
                    if (obs.Category.Count == 0) { obs.Category.Add(catCC); }
                    else { obs.Category[0] = catCC; }
                    error = null;
                    return true;

                case "value":
                    obs.Value = AsDataType(value);
                    error = null;
                    return true;

                case "effective":
                    obs.Effective = AsDataType(value);
                    error = null;
                    return true;

                case "note.text":
                    if (obs.Note.Count == 0) { obs.Note.Add(new Annotation()); }
                    obs.Note[0].TextElement = AsMarkdown(value);
                    error = null;
                    return true;

                case "performer":
                    obs.Performer.Add(AsReference(value));
                    error = null;
                    return true;

                case "partOf":
                    obs.PartOf.Add(AsReference(value));
                    error = null;
                    return true;

                case "basedOn":
                    obs.BasedOn.Add(AsReference(value));
                    error = null;
                    return true;

                case "hasMember":
                    obs.HasMember.Add(AsReference(value));
                    error = null;
                    return true;

                case "encounter":
                    obs.Encounter = AsReference(value);
                    error = null;
                    return true;

                case "encounter.identifier":
                    obs.Encounter ??= new ResourceReference();
                    obs.Encounter.Identifier = AsIdentifier(value);
                    error = null;
                    return true;

                case "encounter.reference":
                    obs.Encounter ??= new ResourceReference();
                    obs.Encounter.Reference = AsString(value);
                    error = null;
                    return true;

                case "code.coding[0].code":
                    obs.Code ??= new CodeableConcept();
                    if (obs.Code.Coding.Count == 0) { obs.Code.Coding.Add(new Coding()); }
                    obs.Code.Coding[0].Code = AsString(value);
                    error = null;
                    return true;

                case "code.coding[0].system":
                    obs.Code ??= new CodeableConcept();
                    if (obs.Code.Coding.Count == 0) { obs.Code.Coding.Add(new Coding()); }
                    obs.Code.Coding[0].System = AsString(value);
                    error = null;
                    return true;

                case "code.coding[0].display":
                    obs.Code ??= new CodeableConcept();
                    if (obs.Code.Coding.Count == 0) { obs.Code.Coding.Add(new Coding()); }
                    obs.Code.Coding[0].Display = AsString(value);
                    error = null;
                    return true;

                case "category[0].coding[0].code":
                    EnsureCategoryCoding(obs).Code = AsString(value);
                    error = null;
                    return true;

                case "category[0].coding[0].system":
                    EnsureCategoryCoding(obs).System = AsString(value);
                    error = null;
                    return true;

                case "category[0].coding[0].display":
                    EnsureCategoryCoding(obs).Display = AsString(value);
                    error = null;
                    return true;

                default:
                    // Fallback: any `<x>.coding[<n>].<field>` shape walks
                    // the named CodeableConcept and assigns the named field
                    // on Coding[<n>] in place.
                    if (TrySetNestedCodingField(obs, p, value))
                    {
                        error = null;
                        return true;
                    }
                    error = $"R4BAdapter: unknown Observation path '{path}' (normalized '{p}').";
                    return false;
            }
        }
        catch (InvalidCastException ex)
        {
            error = $"R4BAdapter: cannot coerce value for path '{path}': {ex.Message}";
            return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetValue(object resource, string path, out object? value)
    {
        if (resource is not Observation obs)
        {
            value = null;
            return false;
        }

        string p = StripResourcePrefix(path);
        switch (p)
        {
            case "code":
                value = obs.Code;
                return obs.Code is not null;

            case "category":
                value = obs.Category.Count > 0 ? obs.Category[0] : null;
                return obs.Category.Count > 0;

            case "value":
                value = obs.Value;
                return obs.Value is not null;

            case "effective":
                value = obs.Effective;
                return obs.Effective is not null;

            case "note.text":
                if (obs.Note.Count > 0 && obs.Note[0].TextElement is Markdown md)
                {
                    value = md.Value;
                    return md.Value is not null;
                }
                value = null;
                return false;

            case "performer":
                value = obs.Performer.Count > 0 ? obs.Performer[0] : null;
                return obs.Performer.Count > 0;

            case "partOf":
                value = obs.PartOf.Count > 0 ? obs.PartOf[0] : null;
                return obs.PartOf.Count > 0;

            case "basedOn":
                value = obs.BasedOn.Count > 0 ? obs.BasedOn[0] : null;
                return obs.BasedOn.Count > 0;

            case "hasMember":
                value = obs.HasMember.Count > 0 ? obs.HasMember[0] : null;
                return obs.HasMember.Count > 0;

            case "encounter":
                value = obs.Encounter;
                return obs.Encounter is not null;

            case "code.coding[0].code":
                if (obs.Code is { Coding.Count: > 0 } cc1)
                {
                    value = cc1.Coding[0].Code;
                    return value is not null;
                }
                value = null;
                return false;

            case "code.coding[0].system":
                if (obs.Code is { Coding.Count: > 0 } cc2)
                {
                    value = cc2.Coding[0].System;
                    return value is not null;
                }
                value = null;
                return false;

            case "category[0].coding[0].code":
                if (obs.Category.Count > 0 && obs.Category[0] is { Coding.Count: > 0 } cat1)
                {
                    value = cat1.Coding[0].Code;
                    return value is not null;
                }
                value = null;
                return false;

            case "category[0].coding[0].system":
                if (obs.Category.Count > 0 && obs.Category[0] is { Coding.Count: > 0 } cat2)
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

    /// <summary>
    /// Strip the leading <c>$resource.</c> and/or <c>Observation.</c>
    /// prefix from a FHIRconnect rule path so the switch arms can
    /// match on the structural tail (<c>note.text</c>, <c>value</c>,
    /// etc.) regardless of whether the caller has already normalised
    /// the path.
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

    private static CodeableConcept AsCodeableConcept(object? value)
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

    private static DataType AsDataType(object? value)
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

    private static Markdown AsMarkdown(object? value)
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

    private static ResourceReference AsReference(object? value)
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

    private static Identifier AsIdentifier(object? value)
    {
        return value switch
        {
            null => new Identifier(),
            Identifier id => id,
            string s => new Identifier { Value = s },
            _ => new Identifier { Value = value.ToString() },
        };
    }

    private static string? AsString(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            _ => value.ToString(),
        };
    }

    private static Coding EnsureCategoryCoding(Observation obs)
    {
        if (obs.Category.Count == 0)
        {
            obs.Category.Add(new CodeableConcept());
        }
        CodeableConcept cc = obs.Category[0];
        if (cc.Coding.Count == 0)
        {
            cc.Coding.Add(new Coding());
        }
        return cc.Coding[0];
    }

    private static readonly System.Text.RegularExpressions.Regex s_nestedCodingPath =
        new System.Text.RegularExpressions.Regex(
            @"^(?<parent>code|category)(?:\[(?<pidx>\d+)\])?\.coding(?:\[(?<idx>\d+)\])?\.(?<field>code|system|display)$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool TrySetNestedCodingField(Observation obs, string normalizedPath, object? value)
    {
        System.Text.RegularExpressions.Match m = s_nestedCodingPath.Match(normalizedPath);
        if (!m.Success)
        {
            return false;
        }
        int codingIdx = m.Groups["idx"].Success
            ? int.Parse(m.Groups["idx"].Value, System.Globalization.CultureInfo.InvariantCulture)
            : 0;
        int parentIdx = m.Groups["pidx"].Success
            ? int.Parse(m.Groups["pidx"].Value, System.Globalization.CultureInfo.InvariantCulture)
            : 0;
        CodeableConcept? cc;
        if (string.Equals(m.Groups["parent"].Value, "code", System.StringComparison.Ordinal))
        {
            obs.Code ??= new CodeableConcept();
            cc = obs.Code;
        }
        else
        {
            while (obs.Category.Count <= parentIdx)
            {
                obs.Category.Add(new CodeableConcept());
            }
            cc = obs.Category[parentIdx];
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
}
