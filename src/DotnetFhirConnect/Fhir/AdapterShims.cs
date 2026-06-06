using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Model;

namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Release-agnostic seam over the per-release Observation POCO that
/// <see cref="AdapterCore"/> uses to read and write fields the v0.x
/// vital-status bundle touches. Each release ships its own
/// implementation (<c>R4ObservationShim</c> / <c>R4BObservationShim</c>
/// / <c>R5ObservationShim</c>) under
/// <c>DotnetFhirConnect.Fhir.{R4,R4B,R5}</c>. The shim stays internal:
/// callers see <see cref="IFhirAdapter"/>, not the shim.
/// </summary>
/// <remarks>
/// All <c>obs</c> parameters are boxed because each release's
/// <c>Observation</c> type lives in a different Firely assembly with
/// no shared base class. The shim is responsible for the cast.
/// </remarks>
internal interface IObservationShim
{
    /// <summary>The textual type name (always <c>"Observation"</c> in v0.x).</summary>
    string TypeName { get; }

    /// <summary>Allocate an empty release-typed Observation.</summary>
    object CreateObservation();

    /// <summary>True if <paramref name="resource"/> is the shim's release-typed Observation.</summary>
    bool IsObservation(object? resource);

    /// <summary>Set Observation.Code to <paramref name="value"/>.</summary>
    void SetCode(object obs, CodeableConcept value);

    /// <summary>Set or replace Observation.Category[0].</summary>
    void SetCategory0(object obs, CodeableConcept value);

    /// <summary>Set Observation.Value (choice DataType).</summary>
    void SetValue(object obs, DataType value);

    /// <summary>Set Observation.Effective (choice DataType).</summary>
    void SetEffective(object obs, DataType value);

    /// <summary>Set Observation.Note[0].Text to <paramref name="text"/>.</summary>
    void SetNote0Text(object obs, Markdown text);

    /// <summary>Append a ResourceReference to Observation.Performer.</summary>
    void AddPerformer(object obs, ResourceReference value);

    /// <summary>Append a ResourceReference to Observation.PartOf.</summary>
    void AddPartOf(object obs, ResourceReference value);

    /// <summary>Append a ResourceReference to Observation.BasedOn.</summary>
    void AddBasedOn(object obs, ResourceReference value);

    /// <summary>Append a ResourceReference to Observation.HasMember.</summary>
    void AddHasMember(object obs, ResourceReference value);

    /// <summary>Set Observation.Encounter.</summary>
    void SetEncounter(object obs, ResourceReference value);

    /// <summary>Ensure Observation.Encounter exists and set its Identifier.</summary>
    void SetEncounterIdentifier(object obs, Identifier value);

    /// <summary>Ensure Observation.Encounter exists and set its Reference string.</summary>
    void SetEncounterReference(object obs, string? value);

    /// <summary>Get Observation.Code (boxed).</summary>
    CodeableConcept? GetCode(object obs);

    /// <summary>Get Observation.Category[0] (boxed) — null if empty.</summary>
    CodeableConcept? GetCategory0(object obs);

    /// <summary>Get Observation.Value (boxed).</summary>
    DataType? GetValue(object obs);

    /// <summary>Get Observation.Effective (boxed).</summary>
    DataType? GetEffective(object obs);

    /// <summary>Get the Markdown text element of Observation.Note[0] (boxed) — null if note empty or unset.</summary>
    Markdown? GetNote0Text(object obs);

    /// <summary>Get Observation.Performer[0] (boxed) — null if empty.</summary>
    ResourceReference? GetPerformer0(object obs);

    /// <summary>Get Observation.PartOf[0] (boxed) — null if empty.</summary>
    ResourceReference? GetPartOf0(object obs);

    /// <summary>Get Observation.BasedOn[0] (boxed) — null if empty.</summary>
    ResourceReference? GetBasedOn0(object obs);

    /// <summary>Get Observation.HasMember[0] (boxed) — null if empty.</summary>
    ResourceReference? GetHasMember0(object obs);

    /// <summary>Get Observation.Encounter (boxed).</summary>
    ResourceReference? GetEncounter(object obs);

    /// <summary>
    /// Serialize a release-typed Resource to JSON. The boxed
    /// <paramref name="resource"/> must be the shim's release.
    /// </summary>
    [RequiresUnreferencedCode("See IFhirAdapter.SerializeResource.")]
    string SerializeResource(object resource);

    /// <summary>
    /// Parse a release-typed Resource from JSON. Returns a boxed
    /// release-typed Resource.
    /// </summary>
    [RequiresUnreferencedCode("See IFhirAdapter.ParseResource.")]
    object ParseResource(System.ReadOnlySpan<char> json);
}
