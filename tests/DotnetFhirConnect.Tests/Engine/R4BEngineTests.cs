extern alias coreR4B;
using System.IO;
using DotnetFhirConnect.Fhir.R4B;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using FhirObservation = coreR4B::Hl7.Fhir.Model.Observation;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Coverage for the typed <see cref="R4BEngine"/> facade: construction
/// validates the bundle's release, and <c>ToFhir</c> returns an R4B
/// <c>Observation</c> POCO. Mirrors the existing R4 coverage.
/// </summary>
public sealed class R4BEngineTests
{
    private static string FixtureRoot(string release) => Path.Combine(
        System.AppContext.BaseDirectory, "fixtures", "cross-release-smoke", release);

    [Fact]
    public void Construct_WithR4BBundle_Succeeds()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r4b"));
        R4BEngine engine = new R4BEngine(bundle);
        Assert.Equal(DotnetFhirConnect.Fhir.FhirRelease.R4B, engine.Core.Adapter.Release);
    }

    [Fact]
    public void Construct_WithR4Bundle_Throws()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        Assert.Throws<System.InvalidOperationException>(() => new R4BEngine(bundle));
    }

    [Fact]
    public void ToFhir_ReturnsR4BObservation()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r4b"));
        OpenEhrComposition composition = EngineFixtures.LoadComposition();

        R4BEngine engine = new R4BEngine(bundle);
        Hl7.Fhir.Model.Resource resource = engine.ToFhir(composition);

        Assert.IsType<FhirObservation>(resource);
        Assert.Equal("Hl7.Fhir.R4B", resource.GetType().Assembly.GetName().Name);
    }
}
