extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System.Linq;
using DotnetFhirConnect;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using FhirObservation = coreR4::Hl7.Fhir.Model.Observation;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 6a happy-path test: the vital_status walking-skeleton
/// composition → R4 Observation. Asserts every field driven by a
/// MODEL rule (not extension rules — those are Phase 6b).
/// </summary>
public sealed class ToFhirTests
{
    [Fact]
    public void VitalStatus_TypedComposition_MapsAllModelRuleFields()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition composition = EngineFixtures.LoadComposition();

        R4Engine engine = new R4Engine(bundle);
        Resource resource = engine.ToFhir(composition);

        FhirObservation obs = Assert.IsType<FhirObservation>(resource);

        // effective rule: $resource.effective ← $archetype/protocol[at0002]/items[at0018]
        Assert.NotNull(obs.Effective);
        FhirDateTime effective = Assert.IsType<FhirDateTime>(obs.Effective);
        Assert.StartsWith("2026-02-19T21:14:54", effective.Value);

        // performer rules — health_care_facility, participations (1 entry),
        // composer. The model file's archetype-side `performer` and
        // `other_participations` paths do not resolve in our fixture
        // (typo `perfomer` in upstream + empty other_participations); we
        // tolerate those. End state should still have 3 performer entries.
        Assert.NotNull(obs.Performer);
        string?[] performerDisplays = obs.Performer.Select(p => p.Display).ToArray();
        Assert.Contains("Test Hospital", performerDisplays);
        Assert.Contains("Performer One", performerDisplays);
        Assert.Contains("Dr. Test Composer", performerDisplays);

        // vitalStatus rule: $resource.value ← $archetype/data[at0001]/items[at0006]
        Assert.NotNull(obs.Value);
        CodeableConcept value = Assert.IsType<CodeableConcept>(obs.Value);
        Coding coding = Assert.Single(value.Coding);
        Assert.Equal("at0007", coding.Code);

        // note rule: $resource.note.text ← $archetype/data[at0001]/items[at0013]
        Assert.NotEmpty(obs.Note);
        Assert.Equal("Patient is alive and well.", obs.Note[0].TextElement?.Value);

        // partOfReference (link, type=partOf) rule: should rewrite
        // the ehr:///compositions/<uuid> link target to
        // Observation/<uuid>.
        Assert.NotEmpty(obs.PartOf);
        Assert.Equal("Observation/00000000-0000-0000-0000-000000000001", obs.PartOf[0].Reference);

        // partOfReference (link, type=basedOn) rule: same rewrite into
        // a second collection. Proves the multi-element /links path is
        // actually walked (the canonical fixture carries 2 LINK
        // entries: partOf + basedOn).
        Assert.NotEmpty(obs.BasedOn);
        Assert.Equal("Observation/00000000-0000-0000-0000-000000000002", obs.BasedOn[0].Reference);
    }

    [Fact]
    public void EngineSelectsR4Adapter_FromVitalStatusBundle()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        FhirConnectEngine engine = new FhirConnectEngine(bundle);
        Assert.Equal(FhirRelease.R4, engine.Adapter.Release);
        Assert.IsType<R4Adapter>(engine.Adapter);
    }
}
