extern alias coreR4B;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4B;
using Xunit;
using Observation = coreR4B::Hl7.Fhir.Model.Observation;
using CodeableConcept = Hl7.Fhir.Model.CodeableConcept;
using Coding = Hl7.Fhir.Model.Coding;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 9 — minimal smoke + switch-arm coverage on
/// <see cref="R4BAdapter"/>. The body is copy-aliased from
/// <see cref="DotnetFhirConnect.Fhir.R4.R4Adapter"/>; tests confirm
/// the alias plumbing returns instances of the R4B
/// <c>Observation</c> POCO, not R4 or R5.
/// </summary>
public sealed class R4BAdapterTests
{
    [Fact]
    public void Release_ReturnsR4B()
    {
        R4BAdapter adapter = new R4BAdapter();
        Assert.Equal(FhirRelease.R4B, adapter.Release);
    }

    [Fact]
    public void CreateResource_Observation_ReturnsR4BInstance()
    {
        R4BAdapter adapter = new R4BAdapter();
        object o = adapter.CreateResource("Observation");
        Observation obs = Assert.IsType<Observation>(o);
        Assert.Equal("Hl7.Fhir.R4B", obs.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void TrySetValue_OnCode_AssignsCodeableConcept()
    {
        R4BAdapter adapter = new R4BAdapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");
        Assert.True(adapter.TrySetValue(obs, "code", new CodeableConcept(system: "http://x", code: "abc"), out string? err), err);
        Assert.NotNull(obs.Code);
        Assert.Equal("abc", obs.Code.Coding[0].Code);
    }

    [Fact]
    public void TrySetValue_OnNoteText_AssignsMarkdown()
    {
        R4BAdapter adapter = new R4BAdapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");
        Assert.True(adapter.TrySetValue(obs, "note.text", "hello", out string? err), err);
        Assert.Single(obs.Note);
    }
}
