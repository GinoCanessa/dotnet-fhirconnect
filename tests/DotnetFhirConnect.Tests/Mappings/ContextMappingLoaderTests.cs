using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Loader tests for <c>KDS_Vitalstatus.context.yaml</c> — confirms
/// profile URL, template id, archetype list, and start archetype.
/// </summary>
public sealed class ContextMappingLoaderTests
{
    [Fact]
    public void Load_KdsContext_ProducesTypedRecord()
    {
        object loaded = FhirConnectMapping.Load(FixtureLocator.ContextFile);

        ContextMapping context = Assert.IsType<ContextMapping>(loaded);
        Assert.Equal(FhirConnectGrammar.V1_0_0, context.Grammar);
        Assert.Equal(MappingType.Context, context.Type);
        Assert.Equal("KDS_Vitalstatus.context", context.Metadata.Name.Trim());

        Assert.Equal(
            "https://www.medizininformatik-initiative.de/fhir/core/modul-person/StructureDefinition/Vitalstatus",
            context.Context.Profile.Url);
        Assert.Equal("2025.0.0", context.Context.Profile.Version);

        Assert.Equal("KDS_Vitalstatus", context.Context.Template.Id);
        Assert.Equal("15.1.0", context.Context.Template.SemVer);

        Assert.Equal("EVALUATION.vital_status.v1", context.Context.Start);
        Assert.Contains("EVALUATION.vital_status.v1", context.Context.Archetypes);
        Assert.Contains("CLUSTER.case_identification.v0", context.Context.Archetypes);
        Assert.Contains("COMPOSITION.report.v1.Observation", context.Context.Archetypes);

        Assert.Contains("KDS_vital_status", context.Context.Extensions);
        Assert.Contains("KDS_composition", context.Context.Extensions);
    }
}
