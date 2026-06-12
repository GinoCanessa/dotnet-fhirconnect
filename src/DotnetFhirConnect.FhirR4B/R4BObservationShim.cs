using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DotnetFhirConnect.Fhir.R4B;

/// <summary>
/// R4B implementation of <see cref="IObservationShim"/>. See
/// <c>R4ObservationShim</c> for the contract.
/// </summary>
internal sealed class R4BObservationShim : IObservationShim
{
    private static readonly Hl7.Fhir.Introspection.ModelInspector s_inspector =
        Hl7.Fhir.Model.ModelInfo.ModelInspector;
    private static readonly BaseFhirJsonDeserializer s_deserializer =
        new BaseFhirJsonDeserializer(s_inspector);
    private static readonly BaseFhirJsonSerializer s_serializer =
        new BaseFhirJsonSerializer(s_inspector);

    public string TypeName => "Observation";

    public object CreateObservation() => new Observation();

    public bool IsObservation(object? resource) => resource is Observation;

    public void SetCode(object obs, CodeableConcept value) =>
        ((Observation)obs).Code = value;

    public void SetCategory0(object obs, CodeableConcept value)
    {
        Observation o = (Observation)obs;
        if (o.Category.Count == 0) { o.Category.Add(value); }
        else { o.Category[0] = value; }
    }

    public void SetValue(object obs, DataType value) =>
        ((Observation)obs).Value = value;

    public void SetEffective(object obs, DataType value) =>
        ((Observation)obs).Effective = value;

    public void SetNote0Text(object obs, Markdown text)
    {
        Observation o = (Observation)obs;
        if (o.Note.Count == 0) { o.Note.Add(new Annotation()); }
        o.Note[0].TextElement = text;
    }

    public void AddPerformer(object obs, ResourceReference value) =>
        ((Observation)obs).Performer.Add(value);

    public void AddPartOf(object obs, ResourceReference value) =>
        ((Observation)obs).PartOf.Add(value);

    public void AddBasedOn(object obs, ResourceReference value) =>
        ((Observation)obs).BasedOn.Add(value);

    public void AddHasMember(object obs, ResourceReference value) =>
        ((Observation)obs).HasMember.Add(value);

    public void SetEncounter(object obs, ResourceReference value) =>
        ((Observation)obs).Encounter = value;

    public void SetEncounterIdentifier(object obs, Identifier value)
    {
        Observation o = (Observation)obs;
        o.Encounter ??= new ResourceReference();
        o.Encounter.Identifier = value;
    }

    public void SetEncounterReference(object obs, string? value)
    {
        Observation o = (Observation)obs;
        o.Encounter ??= new ResourceReference();
        o.Encounter.Reference = value;
    }

    public CodeableConcept? GetCode(object obs) => ((Observation)obs).Code;

    public CodeableConcept? GetCategory0(object obs)
    {
        Observation o = (Observation)obs;
        return o.Category.Count > 0 ? o.Category[0] : null;
    }

    public DataType? GetValue(object obs) => ((Observation)obs).Value;

    public DataType? GetEffective(object obs) => ((Observation)obs).Effective;

    public Markdown? GetNote0Text(object obs)
    {
        Observation o = (Observation)obs;
        return o.Note.Count > 0 ? o.Note[0].TextElement : null;
    }

    public ResourceReference? GetPerformer0(object obs)
    {
        Observation o = (Observation)obs;
        return o.Performer.Count > 0 ? o.Performer[0] : null;
    }

    public ResourceReference? GetPartOf0(object obs)
    {
        Observation o = (Observation)obs;
        return o.PartOf.Count > 0 ? o.PartOf[0] : null;
    }

    public ResourceReference? GetBasedOn0(object obs)
    {
        Observation o = (Observation)obs;
        return o.BasedOn.Count > 0 ? o.BasedOn[0] : null;
    }

    public ResourceReference? GetHasMember0(object obs)
    {
        Observation o = (Observation)obs;
        return o.HasMember.Count > 0 ? o.HasMember[0] : null;
    }

    public ResourceReference? GetEncounter(object obs) => ((Observation)obs).Encounter;

    [RequiresUnreferencedCode(
        "Hl7.Fhir.R4B serializers traverse the typed POCO graph and are not currently AOT-clean. "
        + "Library is not AOT-publishable in v0.x.")]
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

    [RequiresUnreferencedCode("See SerializeResource.")]
    public object ParseResource(ReadOnlySpan<char> json)
    {
        Utf8JsonReader reader = new Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(json.ToString()));
        return s_deserializer.DeserializeResource(ref reader);
    }
}
