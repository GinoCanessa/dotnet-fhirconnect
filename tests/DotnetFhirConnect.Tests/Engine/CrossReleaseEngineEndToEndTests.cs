extern alias coreR4B;
extern alias coreR5;
using System.IO;
using DotnetFhirConnect;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// End-to-end coverage that <see cref="FhirConnectEngine"/> picks
/// the per-release adapter from a bundle's <c>spec.version</c>
/// (<c>R4B</c> / <c>R5</c>) and returns an <c>Observation</c> POCO
/// from the matching Firely assembly. Complements
/// <c>CrossReleaseParseSmokeTests</c>, which covers the adapter
/// parse seam in isolation.
/// </summary>
public sealed class CrossReleaseEngineEndToEndTests
{
    private static string FixtureRoot(string release) => Path.Combine(
        System.AppContext.BaseDirectory, "fixtures", "cross-release-smoke", release);

    [Fact]
    public void EngineSelectsR4BAdapter_FromR4BBundle()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r4b"));
        OpenEhrComposition composition = EngineFixtures.LoadComposition();

        FhirConnectEngine engine = new FhirConnectEngine(bundle);
        object produced = engine.ToFhir(composition);

        Assert.Equal("Hl7.Fhir.R4B", produced.GetType().Assembly.GetName().Name);
        Assert.IsType<coreR4B::Hl7.Fhir.Model.Observation>(produced);
    }

    [Fact]
    public void EngineSelectsR5Adapter_FromR5Bundle()
    {
        MappingBundle bundle = MappingBundle.Load(FixtureRoot("r5"));
        OpenEhrComposition composition = EngineFixtures.LoadComposition();

        FhirConnectEngine engine = new FhirConnectEngine(bundle);
        object produced = engine.ToFhir(composition);

        Assert.Equal("Hl7.Fhir.R5", produced.GetType().Assembly.GetName().Name);
        Assert.IsType<coreR5::Hl7.Fhir.Model.Observation>(produced);
    }
}
