using System.Linq;
using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Loader tests for <c>vital_status.v1.yml</c> — confirms the 13
/// rules (8 distinct names + 5 partOfReference variants), the typed
/// projections of every rule shape used by the model file, and the
/// archetype / FHIR config wiring.
/// </summary>
public sealed class ModelMappingLoaderTests
{
    [Fact]
    public void Load_VitalStatusModel_ProducesTypedRecord()
    {
        object loaded = FhirConnectMapping.Load(FixtureLocator.ModelFile);

        ModelMapping model = Assert.IsType<ModelMapping>(loaded);
        Assert.Equal(FhirConnectGrammar.V1_0_0, model.Grammar);
        Assert.Equal(MappingType.Model, model.Type);
        Assert.Equal("EVALUATION.vital_status.v1", model.Metadata.Name);

        Assert.Equal("FHIR", model.Spec.System);
        Assert.Equal(FhirRelease.R4, model.Spec.Version);
        Assert.NotNull(model.Spec.OpenEhrConfig);
        Assert.Equal(
            "openEHR-EHR-EVALUATION.vital_status.v1",
            model.Spec.OpenEhrConfig!.Archetype);
        Assert.NotNull(model.Spec.FhirConfig);
        Assert.Equal(
            "http://hl7.org/fhir/StructureDefinition/Observation",
            model.Spec.FhirConfig!.StructureDefinition);
    }

    [Fact]
    public void Load_VitalStatusModel_HasThirteenRules()
    {
        ModelMapping model = (ModelMapping)FhirConnectMapping.Load(FixtureLocator.ModelFile);

        Assert.Equal(13, model.Mappings.Count);

        // 8 distinct rule names + 5 partOfReference variants.
        int partOfRefCount = model.Mappings.Count(r => r.Name == "partOfReference");
        Assert.Equal(5, partOfRefCount);

        string[] distinctNames = model.Mappings.Select(r => r.Name).Distinct().ToArray();
        Assert.Equal(9, distinctNames.Length); // 8 unique + the partOfReference itself
        Assert.Contains("healthCareFacility", distinctNames);
        Assert.Contains("participations", distinctNames);
        Assert.Contains("composer", distinctNames);
        Assert.Contains("performer", distinctNames);
        Assert.Contains("other_participations", distinctNames);
        Assert.Contains("effective", distinctNames);
        Assert.Contains("vitalStatus", distinctNames);
        Assert.Contains("note", distinctNames);
        Assert.Contains("partOfReference", distinctNames);
    }

    [Fact]
    public void Load_VitalStatusModel_ParticipationsHasNoneTypeAndTwoFollowedBy()
    {
        ModelMapping model = (ModelMapping)FhirConnectMapping.Load(FixtureLocator.ModelFile);

        MappingRule participations = model.Mappings.Single(r => r.Name == "participations");
        Assert.Equal(WithType.None, participations.With.Type);
        Assert.NotNull(participations.FollowedBy);
        Assert.Equal(2, participations.FollowedBy!.Mappings.Count);

        MappingRule participationFunction = participations.FollowedBy.Mappings[0];
        Assert.Equal("participationFunction", participationFunction.Name);
        Assert.NotNull(participationFunction.Manual);
        Assert.Single(participationFunction.Manual!);
        Assert.NotNull(participationFunction.Manual![0].OpenEhr);
        Assert.Equal("function", participationFunction.Manual![0].OpenEhr![0].Path);
        Assert.Equal("performer", participationFunction.Manual![0].OpenEhr![0].Value);
    }

    [Fact]
    public void Load_VitalStatusModel_PartOfReferenceVariantsCarryDistinctLinkMetadata()
    {
        ModelMapping model = (ModelMapping)FhirConnectMapping.Load(FixtureLocator.ModelFile);

        MappingRule[] partOfRefs = model.Mappings.Where(r => r.Name == "partOfReference").ToArray();
        Assert.Equal(5, partOfRefs.Length);

        Assert.All(partOfRefs, r => Assert.NotNull(r.Link));

        string[] linkTypes = partOfRefs.Select(r => r.Link!.Type).Distinct().ToArray();
        Assert.Equal(5, linkTypes.Length);
        Assert.Contains("partOf", linkTypes);
        Assert.Contains("basedOn", linkTypes);
        Assert.Contains("focus", linkTypes);
        Assert.Contains("case", linkTypes);
        Assert.Contains("hasMember", linkTypes);
    }
}
