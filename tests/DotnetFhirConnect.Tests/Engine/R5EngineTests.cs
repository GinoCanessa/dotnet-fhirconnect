extern alias coreR5;
using System.IO;
using DotnetFhirConnect.Fhir.R5;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using FhirObservation = coreR5::Hl7.Fhir.Model.Observation;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Coverage for the typed <see cref="R5Engine"/> facade: construction
/// validates the bundle's release, and <c>ToFhir</c> returns an R5
/// <c>Observation</c> POCO. Mirrors the existing R4 coverage.
/// </summary>
public sealed class R5EngineTests
{
    private static string FixtureRoot(string release) => Path.Combine(
        System.AppContext.BaseDirectory, "fixtures", "cross-release-smoke", release);

    [Fact]
    public void Construct_WithR5Bundle_Succeeds()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r5"));
        R5Engine engine = new R5Engine(bundle);
        Assert.Equal(DotnetFhirConnect.Fhir.FhirRelease.R5, engine.Core.Adapter.Release);
    }

    [Fact]
    public void Construct_WithR4Bundle_Throws()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        Assert.Throws<System.InvalidOperationException>(() => new R5Engine(bundle));
    }

    [Fact]
    public void ToFhir_ReturnsR5Observation()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r5"));
        OpenEhrComposition composition = EngineFixtures.LoadComposition();

        R5Engine engine = new R5Engine(bundle);
        Hl7.Fhir.Model.Resource resource = engine.ToFhir(composition);

        Assert.IsType<FhirObservation>(resource);
        Assert.Equal("Hl7.Fhir.R5", resource.GetType().Assembly.GetName().Name);
    }
}
