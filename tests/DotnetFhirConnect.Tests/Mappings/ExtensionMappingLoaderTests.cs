using System.Linq;
using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Loader tests for the two extension files in the vital_status
/// bundle. Pins the typed shape of <see cref="ExtensionAction"/>,
/// nested <see cref="ReferenceSpec"/> rules, the
/// <see cref="LinkSpec"/> on the encounter mapping, and the
/// "either side of <c>with</c> may be null" data model requirement.
/// </summary>
public sealed class ExtensionMappingLoaderTests
{
    [Fact]
    public void Load_KdsComposition_HasOverwriteAndAddRules()
    {
        ExtensionMapping ext = (ExtensionMapping)FhirConnectMapping.Load(FixtureLocator.KdsCompositionFile);

        Assert.Equal(MappingType.Extension, ext.Type);
        Assert.Equal("KDS_composition", ext.Metadata.Name);
        Assert.Equal("COMPOSITION.report.v1.Observation", ext.Spec.Extends);
        Assert.Equal(2, ext.Mappings.Count);

        MappingRule encounter = ext.Mappings.Single(r => r.Name == "encounter");
        Assert.Equal(ExtensionAction.Overwrite, encounter.Extension);
        Assert.Equal("CLUSTER.case_identification.v0", encounter.SlotArchetype);

        MappingRule fallId = ext.Mappings.Single(r => r.Name == "fallIdentifikationReference");
        Assert.Equal(ExtensionAction.Add, fallId.Extension);
        Assert.NotNull(fallId.Reference);
        Assert.Equal("Encounter", fallId.Reference!.ResourceType);
        Assert.Equal(2, fallId.Reference.Mappings.Count);

        MappingRule encMapping = fallId.Reference.Mappings.Single(r => r.Name == "encounterMapping");
        Assert.NotNull(encMapping.Link);
        Assert.Equal("the case this composition relates to", encMapping.Link!.Meaning);
        Assert.Equal("case", encMapping.Link.Type);
    }

    [Fact]
    public void Load_KdsVitalStatus_HasCompositionMappingAndCodeMappingsWithCodingNullOpenEhr()
    {
        ExtensionMapping ext = (ExtensionMapping)FhirConnectMapping.Load(FixtureLocator.KdsVitalsignsFile);

        Assert.Equal("KDS_vital_status", ext.Metadata.Name);
        Assert.Equal("EVALUATION.vital_status.v1", ext.Spec.Extends);
        Assert.Equal(3, ext.Mappings.Count);

        MappingRule compositionMapping = ext.Mappings.Single(r => r.Name == "compositionMapping");
        Assert.Equal(ExtensionAction.Add, compositionMapping.Extension);
        Assert.Equal("COMPOSITION.report.v1.Observation", compositionMapping.SlotArchetype);

        // The 'category' and 'code' rules each carry a nested followedBy
        // whose child rule has `with: { fhir: "coding" }` — no openehr key.
        // Pinning the "Fhir or OpenEhr may be null" data-model requirement.
        MappingRule category = ext.Mappings.Single(r => r.Name == "category");
        Assert.NotNull(category.FollowedBy);
        Assert.Single(category.FollowedBy!.Mappings);

        MappingRule categoryManual = category.FollowedBy.Mappings[0];
        Assert.Equal("coding", categoryManual.With.Fhir);
        Assert.Null(categoryManual.With.OpenEhr);

        MappingRule code = ext.Mappings.Single(r => r.Name == "code");
        Assert.NotNull(code.FollowedBy);
        MappingRule codeManual = code.FollowedBy!.Mappings[0];
        Assert.Equal("coding", codeManual.With.Fhir);
        Assert.Null(codeManual.With.OpenEhr);
        Assert.NotNull(codeManual.Manual);
        Assert.Equal("survey", codeManual.Manual![0].Name);
        // LOINC 67162-8 hard-coded in the extension manual block.
        Assert.Equal("67162-8", codeManual.Manual![0].Fhir!.Single(f => f.Path == "code").Value);
        Assert.Equal("http://loinc.org", codeManual.Manual![0].Fhir!.Single(f => f.Path == "system").Value);
    }
}
