extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System;
using System.Linq;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 8 — black-box acceptance for the KDS extensions: confirm
/// that the produced Observation actually carries the LOINC code
/// (67162-8), survey category, and encounter wiring that
/// KDS_vital_status + KDS_composition inject into the Phase 6a
/// model-only output.
/// </summary>
public sealed class KdsExtensionAcceptanceTests
{
    private static Observation BuildVitalStatusObservation()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition composition = EngineFixtures.LoadComposition();
        R4Engine engine = new R4Engine(bundle);
        Resource produced = engine.ToFhir(composition);
        return Assert.IsType<Observation>(produced);
    }

    [Fact]
    public void KdsVitalStatus_InjectsLoincCode()
    {
        Observation obs = BuildVitalStatusObservation();
        Assert.NotNull(obs.Code);
        Coding coding = Assert.Single(obs.Code.Coding);
        Assert.Equal("67162-8", coding.Code);
        Assert.Equal("http://loinc.org", coding.System);
    }

    [Fact]
    public void KdsVitalStatus_InjectsSurveyCategory()
    {
        Observation obs = BuildVitalStatusObservation();
        CodeableConcept cc = Assert.Single(obs.Category);
        Coding coding = Assert.Single(cc.Coding);
        Assert.Equal("survey", coding.Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/observation-category", coding.System);
    }

    [Fact]
    public void KdsComposition_WiresUpEncounterIdentifier()
    {
        Observation obs = BuildVitalStatusObservation();
        Assert.NotNull(obs.Encounter);
        Assert.NotNull(obs.Encounter.Identifier);
        Assert.Equal("CASE-12345", obs.Encounter.Identifier.Value);
    }

    [Fact]
    public void AllFivePartOfReferenceRulesStillEmit()
    {
        // Regression pin: the Phase 6 composite-key merge must not
        // have collapsed the five same-named partOfReference rules.
        // The canonical composition only carries two LINKs (partOf,
        // basedOn), so at minimum PartOf + BasedOn must be populated;
        // the other three (focus, case, hasMember) have no source
        // data in the fixture and stay empty.
        Observation obs = BuildVitalStatusObservation();
        Assert.NotEmpty(obs.PartOf);
        Assert.NotEmpty(obs.BasedOn);
        Assert.Equal("Observation/00000000-0000-0000-0000-000000000001", obs.PartOf[0].Reference);
        Assert.Equal("Observation/00000000-0000-0000-0000-000000000002", obs.BasedOn[0].Reference);
    }

    [Fact]
    public void RoundTrip_ExtensionInjectedFieldsPersist_AcrossSecondHop()
    {
        // Composition -> Observation -> Composition -> Observation'
        // and assert the extension-injected `code` / `category`
        // fields are byte-equal across the first and second hops.
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition canonical = EngineFixtures.LoadComposition();
        R4Engine engine = new R4Engine(bundle);

        Observation first = Assert.IsType<Observation>(engine.ToFhir(canonical));
        OpenEhrComposition rebuilt = engine.Core.ToOpenEhr(first);
        Observation second = Assert.IsType<Observation>(engine.ToFhir(rebuilt));

        Assert.Equal(first.Code.Coding[0].Code, second.Code.Coding[0].Code);
        Assert.Equal(first.Code.Coding[0].System, second.Code.Coding[0].System);
        Assert.Equal(first.Category[0].Coding[0].Code, second.Category[0].Coding[0].Code);
        Assert.Equal(first.Category[0].Coding[0].System, second.Category[0].Coding[0].System);
    }
}
