extern alias coreR4;
extern alias coreR4B;
extern alias coreR5;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Fhir.R4B;
using DotnetFhirConnect.Fhir.R5;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 9 — runtime gate that parsing a minimal FHIR resource via
/// R4 / R4B / R5 adapters in the same process returns instances
/// from the correct per-release assembly. Catches the failure mode
/// where Firely's reflection-based model discovery picks the wrong
/// POCO at runtime even when the build is clean.
/// </summary>
public sealed class CrossReleaseParseSmokeTests
{
    private const string MinimalObservationJson =
        "{\"resourceType\":\"Observation\",\"status\":\"final\",\"code\":{\"coding\":[{\"system\":\"http://loinc.org\",\"code\":\"67162-8\"}]}}";

    [Fact]
    public void Parse_ViaR4Adapter_ReturnsR4Observation()
    {
        R4Adapter adapter = new R4Adapter();
        object parsed = adapter.ParseResource(MinimalObservationJson.AsSpan());
        Assert.IsType<coreR4::Hl7.Fhir.Model.Observation>(parsed);
        Assert.Equal("Hl7.Fhir.R4", parsed.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Parse_ViaR4BAdapter_ReturnsR4BObservation()
    {
        R4BAdapter adapter = new R4BAdapter();
        object parsed = adapter.ParseResource(MinimalObservationJson.AsSpan());
        Assert.IsType<coreR4B::Hl7.Fhir.Model.Observation>(parsed);
        Assert.Equal("Hl7.Fhir.R4B", parsed.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void Parse_ViaR5Adapter_ReturnsR5Observation()
    {
        R5Adapter adapter = new R5Adapter();
        object parsed = adapter.ParseResource(MinimalObservationJson.AsSpan());
        Assert.IsType<coreR5::Hl7.Fhir.Model.Observation>(parsed);
        Assert.Equal("Hl7.Fhir.R5", parsed.GetType().Assembly.GetName().Name);
    }

    [Fact]
    public void FhirAdapterFactory_Create_ReturnsCorrectReleasePerEnumValue()
    {
        Assert.IsType<R4Adapter>(FhirAdapterFactory.Create(FhirRelease.R4));
        Assert.IsType<R4BAdapter>(FhirAdapterFactory.Create(FhirRelease.R4B));
        Assert.IsType<R5Adapter>(FhirAdapterFactory.Create(FhirRelease.R5));
    }
}
