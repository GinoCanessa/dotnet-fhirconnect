extern alias coreR5;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R5;
using Xunit;
using Observation = coreR5::Hl7.Fhir.Model.Observation;
using CodeableConcept = Hl7.Fhir.Model.CodeableConcept;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 9 — minimal smoke + switch-arm coverage on
/// <see cref="R5Adapter"/>. The body is copy-aliased from
/// <see cref="DotnetFhirConnect.Fhir.R4.R4Adapter"/>; tests confirm
/// the alias plumbing returns instances of the R5
/// <c>Observation</c> POCO, not R4 or R4B. R5's restructured
/// Encounter fields (class / reasonCode rename / subjectStatus) are
/// out of scope per the v0.x plan.
/// </summary>
public sealed class R5AdapterTests
{
    [Fact]
    public void Release_ReturnsR5()
    {
        R5Adapter adapter = new R5Adapter();
        Assert.Equal(FhirRelease.R5, adapter.Release);
    }

    [Fact]
    public void CreateResource_Observation_ReturnsR5Instance()
    {
        R5Adapter adapter = new R5Adapter();
        object o = adapter.CreateResource("Observation");
        Observation obs = Assert.IsType<Observation>(o);
        Assert.Equal("Hl7.Fhir.R5", obs.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void TrySetValue_OnCode_AssignsCodeableConcept()
    {
        R5Adapter adapter = new R5Adapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");
        Assert.True(adapter.TrySetValue(obs, "code", new CodeableConcept(system: "http://x", code: "abc"), out string? err), err);
        Assert.NotNull(obs.Code);
        Assert.Equal("abc", obs.Code.Coding[0].Code);
    }

    [Fact]
    public void TrySetValue_OnNoteText_AssignsMarkdown()
    {
        R5Adapter adapter = new R5Adapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");
        Assert.True(adapter.TrySetValue(obs, "note.text", "hello", out string? err), err);
        Assert.Single(obs.Note);
    }
}
