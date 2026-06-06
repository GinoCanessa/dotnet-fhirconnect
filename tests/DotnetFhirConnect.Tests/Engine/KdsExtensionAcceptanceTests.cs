extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System;
using System.Linq;
using System.Text.Json.Nodes;
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

    /// <summary>
    /// Composition → Observation → Composition → Observation' and
    /// assert the extension-injected `code` / `category` subtrees are
    /// byte-equal across the first and second hops. The check is
    /// scoped to those two subtrees because they are the only fields
    /// the KDS extensions inject *and* the round-trip pipeline
    /// preserves end-to-end: `partOf` / `basedOn` /
    /// `encounter.identifier` are lost on the ToOpenEhr leg today
    /// (the link rules are gated by `_direction == ToFhir` in
    /// `MappingRuleExecutor.ExecuteLink`, and `ToOpenEhr` starts from
    /// an empty `SkeletonBuilder.ForArchetype` so the rebuilt
    /// composition has no Links and no `other_context`
    /// case-identification cluster). A whole-Observation
    /// `Assert.Equal(firstJson, secondJson)` would therefore be red
    /// without ToOpenEhr Link / Links-aware SkeletonBuilder work
    /// that is out of scope for v0.x. The per-field assertions are
    /// kept as a diagnostic shortcut for post-mortem when the
    /// subtree check fails.
    /// </summary>
    [Fact]
    public void RoundTrip_ExtensionInjectedFieldsPersist_AcrossSecondHop()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition canonical = EngineFixtures.LoadComposition();
        R4Engine engine = new R4Engine(bundle);

        Observation first = Assert.IsType<Observation>(engine.ToFhir(canonical));
        OpenEhrComposition rebuilt = engine.Core.ToOpenEhr(first);
        Observation second = Assert.IsType<Observation>(engine.ToFhir(rebuilt));

        string firstJson = engine.Core.Adapter.SerializeResource(first);
        string secondJson = engine.Core.Adapter.SerializeResource(second);
        JsonNode? firstNode = JsonNode.Parse(firstJson);
        JsonNode? secondNode = JsonNode.Parse(secondJson);
        Assert.NotNull(firstNode);
        Assert.NotNull(secondNode);

        // Byte-equal on the `code` and `category` subtrees — the two
        // KDS-injected fields the round-trip pipeline preserves.
        Assert.Equal(
            firstNode!["code"]!.ToJsonString(),
            secondNode!["code"]!.ToJsonString());
        Assert.Equal(
            firstNode!["category"]!.ToJsonString(),
            secondNode!["category"]!.ToJsonString());

        // Diagnostic shortcut — kept so a post-mortem on the
        // subtree-equal failure has a faster path to the offending
        // Coding.
        Assert.Equal(first.Code.Coding[0].Code, second.Code.Coding[0].Code);
        Assert.Equal(first.Code.Coding[0].System, second.Code.Coding[0].System);
        Assert.Equal(first.Category[0].Coding[0].Code, second.Category[0].Coding[0].Code);
        Assert.Equal(first.Category[0].Coding[0].System, second.Category[0].Coding[0].System);
    }
}
