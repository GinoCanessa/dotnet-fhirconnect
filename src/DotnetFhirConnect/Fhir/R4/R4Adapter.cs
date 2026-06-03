using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DotnetFhirConnect.Fhir.R4;

/// <summary>
/// HL7 FHIR R4 implementation of <see cref="IFhirAdapter"/>, built
/// on top of <c>Hl7.Fhir.R4</c>. The path resolver handles only the
/// property graph the v0.x vital_status walking-skeleton mapping
/// touches on <see cref="Observation"/> — extension is straightforward
/// (add a switch arm); routing every FHIR path through
/// <c>FhirPathCompiler</c> is explicitly out of scope for v0.x.
/// </summary>
public sealed class R4Adapter : IFhirAdapter
{
    private static readonly Hl7.Fhir.Introspection.ModelInspector s_inspector =
        Hl7.Fhir.Model.ModelInfo.ModelInspector;
    private static readonly BaseFhirJsonDeserializer s_deserializer =
        new BaseFhirJsonDeserializer(s_inspector);
    private static readonly BaseFhirJsonSerializer s_serializer =
        new BaseFhirJsonSerializer(s_inspector);

    /// <inheritdoc/>
    public FhirRelease Release => FhirRelease.R4;

    /// <inheritdoc/>
    [RequiresUnreferencedCode(
        "Hl7.Fhir.R4 parsers traverse the typed POCO graph and are not currently AOT-clean. "
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
                $"R4Adapter.SerializeResource: expected Hl7.Fhir.R4.Model.Resource, got {resource?.GetType().FullName ?? "null"}.",
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
                $"R4Adapter.CreateResource: type '{typeName}' not registered in v0.x. " +
                "Add a switch arm in R4Adapter or wait for Phase 6b to broaden coverage.",
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
        if (resource is not Observation obs)
        {
            error = $"R4Adapter: only Observation is supported in v0.x; got {resource?.GetType().Name ?? "null"}.";
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

                default:
                    error = $"R4Adapter: unknown Observation path '{path}' (normalized '{p}').";
                    return false;
            }
        }
        catch (InvalidCastException ex)
        {
            error = $"R4Adapter: cannot coerce value for path '{path}': {ex.Message}";
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
}
