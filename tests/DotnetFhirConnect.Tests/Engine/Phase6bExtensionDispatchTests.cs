extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System;
using DotnetFhirConnect;
using DotnetFhirConnect.Engine;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 7 — extension rule dispatch:
/// <c>reference</c> / <c>slotArchetype</c> / FHIRPath normalisation /
/// <c>$reference</c> prefix.
/// </summary>
public sealed class Phase6bExtensionDispatchTests
{
    [Fact]
    public void FhirPath_OfTypeSegmentIsStripped()
    {
        Assert.Equal(
            "$resource.encounter.identifier",
            MappingRuleExecutor.NormalizeFhirPath("$resource.encounter.ofType(Reference).identifier"));
    }

    [Fact]
    public void FhirPath_NoFhirPathFragment_PassesThrough()
    {
        Assert.Equal("$resource.code", MappingRuleExecutor.NormalizeFhirPath("$resource.code"));
    }

    [Fact]
    public void FhirPath_UnsupportedSegment_Throws()
    {
        Assert.Throws<NotSupportedException>(
            () => MappingRuleExecutor.NormalizeFhirPath("$resource.encounter.where(reference='Encounter/1').identifier"));
    }

    [Fact]
    public void DollarReferencePrefix_OutsideReferenceRule_Throws()
    {
        OpenEhrComposition comp = EngineFixtures.LoadComposition();
        Evaluation eval = FindEvaluation(comp);
        BindingContext ctx = new BindingContext(
            Composition: comp,
            Archetype: eval,
            OpenEhrRoot: eval,
            Resource: new object(),
            FhirRoot: "$resource");

        Assert.Throws<InvalidOperationException>(
            () => OpenEhrPathResolver.Resolve(ctx, "$reference/identifier"));
    }

    [Fact]
    public void DollarReferencePrefix_ResolvesAgainstReferenceRoot()
    {
        OpenEhrComposition comp = EngineFixtures.LoadComposition();
        Evaluation eval = FindEvaluation(comp);
        DvEhrUri root = new DvEhrUri { Value = "ehr:///compositions/test-id" };
        BindingContext ctx = new BindingContext(
            Composition: comp,
            Archetype: eval,
            OpenEhrRoot: eval,
            Resource: new object(),
            FhirRoot: "$resource",
            ReferenceRoot: root);

        // Resolving $reference with an empty remainder yields the root.
        object? resolved = OpenEhrPathResolver.Resolve(ctx, "$reference");
        Assert.Same(root, resolved);
    }

    [Fact]
    public void Reference_KdsBundle_EngineRunsWithoutThrowing()
    {
        // Black-box: with KDS_composition's reference rules wired up,
        // ToFhir must complete without throwing on the
        // fallIdentifikationReference/encounter pair.
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition composition = EngineFixtures.LoadComposition();
        R4Engine engine = new R4Engine(bundle);
        Resource produced = engine.ToFhir(composition);
        Observation obs = Assert.IsType<Observation>(produced);
        Assert.NotNull(obs);
    }

    [Fact]
    public void SlotArchetypeOnlyRule_IsNoOp()
    {
        // Synthetic: a rule with slotArchetype set but no manual /
        // followedBy / link / reference / executable openehr+fhir
        // resolution must be a clean no-op (informational binding
        // marker).
        ModelMapping model = SyntheticModel("m", "EVALUATION.synth.v1",
        [
            new MappingRule(
                Name: "marker",
                With: new WithBlock(Fhir: "$resource", OpenEhr: "$composition", Type: WithType.None),
                Unidirectional: null,
                Manual: null,
                FollowedBy: null,
                Link: null,
                Reference: null,
                SlotArchetype: "COMPOSITION.report.v1.Observation",
                Extension: null,
                FhirCondition: null),
        ]);
        MappingBundle bundle = new MappingBundle(
            Context: null,
            Models: new Dictionary<string, ModelMapping> { [model.Metadata.Name] = model },
            Extensions: new Dictionary<string, ExtensionMapping>());

        // SkeletonBuilder only knows vital_status; for this synthetic
        // test we just want to confirm engine construction + ToFhir
        // does not throw on the marker rule. Use a vital_status
        // composition + the engine's R4 adapter directly.
        FhirConnectEngine engine = new FhirConnectEngine(bundle);
        Observation obs = new Observation();
        // Manually drive the executor in a way that hits the marker
        // rule via the model's Mappings list, bypassing SkeletonBuilder.
        OpenEhrComposition skeleton = EngineFixtures.LoadComposition();
        Evaluation eval = FindEvaluation(skeleton);
        BindingContext ctx = new BindingContext(
            Composition: skeleton,
            Archetype: eval,
            OpenEhrRoot: eval,
            Resource: obs,
            FhirRoot: "$resource");
        MappingRuleExecutor executor = new MappingRuleExecutor(engine.Adapter, TransformDirection.ToFhir);
        executor.ExecuteAll(ctx, engine.EffectiveMapping.Rules);
    }

    private static Evaluation FindEvaluation(OpenEhrComposition comp)
    {
        Assert.NotNull(comp.Content);
        foreach (object item in comp.Content!)
        {
            if (item is Evaluation eval)
            {
                return eval;
            }
        }
        throw new InvalidOperationException("Composition has no Evaluation entry.");
    }

    private static ModelMapping SyntheticModel(string name, string archetype, IReadOnlyList<MappingRule> rules) =>
        new ModelMapping(
            grammar: FhirConnectGrammar.V1_0_0,
            metadata: new MappingMetadata(name, "0.0.1"),
            spec: new MappingSpec(
                System: "FHIR",
                Version: FhirRelease.R4,
                Extends: null,
                OpenEhrConfig: new OpenEhrConfig(archetype, null),
                FhirConfig: new FhirConfig("http://hl7.org/fhir/StructureDefinition/Observation")),
            mappings: rules);
}
