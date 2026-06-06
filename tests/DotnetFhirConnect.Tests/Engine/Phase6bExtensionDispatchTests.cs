extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System;
using System.IO;
using System.Linq;
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
    public void Reference_KdsComposition_Encounter_BuildsReference()
    {
        // Narrow regression for the outer reference rule
        // (fallIdentifikationReference in KDS_composition.yml): the
        // executor must hand a ResourceReference value to the adapter
        // at the encounter path (after stripping the `.reference`
        // suffix per MappingRuleExecutor.cs:254). Asserting on
        // `rr.Reference == "Encounter/<id>"` belongs to deferred item
        // 2a — that rule's with.openehr is `$reference` so the
        // executor leaves Reference null in v0.x.
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition composition = EngineFixtures.LoadComposition();
        RecordingFhirAdapter recorder = new RecordingFhirAdapter(new R4Adapter());
        FhirConnectEngine engine = new FhirConnectEngine(bundle, recorder);

        engine.ToFhir(composition);

        bool found = false;
        foreach (RecordingFhirAdapter.Capture c in recorder.Captures)
        {
            if (c.Value is ResourceReference &&
                (c.Path == "$resource.encounter" || c.Path == "Observation.encounter" || c.Path == "encounter"))
            {
                found = true;
                break;
            }
        }
        Assert.True(found, "Expected a TrySetValue capture with a ResourceReference value at the encounter path; got: " +
            string.Join(", ", recorder.Captures.Select(c => $"({c.ResourceType},{c.Path},{c.Value?.GetType().Name ?? "null"})")));
    }

    [Fact]
    public void Reference_KdsComposition_FallIdentifikationIdentifier_PopulatesNestedIdentifier()
    {
        // Inner rule `identifierInReference` under the
        // fallIdentifikationReference reference scope writes
        // `$fhirRoot.identifier` against the ResourceReference. The
        // case_identification cluster carries CASE-12345.
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition composition = EngineFixtures.LoadComposition();
        RecordingFhirAdapter recorder = new RecordingFhirAdapter(new R4Adapter());
        FhirConnectEngine engine = new FhirConnectEngine(bundle, recorder);

        engine.ToFhir(composition);

        RecordingFhirAdapter.Capture? hit = null;
        foreach (RecordingFhirAdapter.Capture c in recorder.Captures)
        {
            if (c.ResourceType == "ResourceReference" &&
                c.Value is Identifier id &&
                string.Equals(id.Value, "CASE-12345", StringComparison.Ordinal))
            {
                hit = c;
                break;
            }
        }
        Assert.True(hit.HasValue, "Expected a ResourceReference TrySetValue capture with Identifier{Value=CASE-12345}; got: " +
            string.Join(", ", recorder.Captures.Select(c => $"({c.ResourceType},{c.Path},{c.Value?.GetType().Name ?? "null"})")));
        string p = hit!.Value.Path;
        Assert.True(
            p == "$resource.identifier" || p == "Observation.identifier" || p == "identifier",
            $"Expected identifier-path capture (post-StripResourcePrefix == 'identifier'); got '{p}'.");
    }

    [Fact]
    public void SlotArchetype_HintIsHonouredForCluster()
    {
        // A rule that carries both slotArchetype AND a real
        // with.openehr / with.fhir pair (with.Type != None) must NOT
        // be silently treated as a marker no-op. The slotArchetype is
        // an informational binding hint; the executor still runs the
        // direct copy. Asserts exactly one TrySetValue capture lands
        // for the synthetic rule's resolved path.
        MappingRule synthetic = new MappingRule(
            Name: "slotArchHintDirect",
            With: new WithBlock(Fhir: "$resource.code", OpenEhr: "$archetype/data[at0001]/items[at0006]", Type: WithType.Default),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: "CLUSTER.fake_hint.v0",
            Extension: null,
            FhirCondition: null);
        ModelMapping model = SyntheticModel("syntheticSlotHint", "openEHR-EHR-EVALUATION.vital_status.v1", [synthetic]);
        MappingBundle bundle = new MappingBundle(
            Context: null,
            Models: new Dictionary<string, ModelMapping> { [model.Metadata.Name] = model },
            Extensions: new Dictionary<string, ExtensionMapping>());

        OpenEhrComposition composition = EngineFixtures.LoadComposition();
        RecordingFhirAdapter recorder = new RecordingFhirAdapter(new R4Adapter());
        FhirConnectEngine engine = new FhirConnectEngine(bundle, recorder);

        engine.ToFhir(composition);

        int captureCount = recorder.Captures.Count;
        Assert.True(
            captureCount == 1,
            $"Expected exactly 1 TrySetValue capture (slotArchetype hint must not suppress a direct rule); got {captureCount}: " +
            string.Join(", ", recorder.Captures.Select(c => $"({c.ResourceType},{c.Path},{c.Value?.GetType().Name ?? "null"})")));
        RecordingFhirAdapter.Capture only = recorder.Captures[0];
        Assert.Equal("$resource.code", only.Path);
        Assert.IsType<CodeableConcept>(only.Value);
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

    [Fact]
    public void RuleClassifier_ClassifiesEveryKind()
    {
        // Direct copy
        MappingRule direct = new MappingRule(
            Name: "d",
            With: new WithBlock(Fhir: "$resource.code", OpenEhr: "$archetype/x", Type: WithType.Default),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.DirectCopy, direct.Kind);

        // Wrapper followedBy
        MappingRule wrapper = new MappingRule(
            Name: "w",
            With: new WithBlock(Fhir: "$resource.x", OpenEhr: "$archetype/x", Type: WithType.None),
            Unidirectional: null,
            Manual: null,
            FollowedBy: new FollowedBy([direct]),
            Link: null,
            Reference: null,
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.WrapperFollowedBy, wrapper.Kind);

        // Manual
        MappingRule manual = new MappingRule(
            Name: "m",
            With: new WithBlock(Fhir: "$resource", OpenEhr: null, Type: WithType.Default),
            Unidirectional: null,
            Manual: [new ManualEntry("e", [new ManualField("x", "v")], null)],
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.Manual, manual.Kind);

        // Link
        MappingRule link = new MappingRule(
            Name: "l",
            With: new WithBlock(Fhir: "$resource.partOf", OpenEhr: "$archetype/links", Type: WithType.Default),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: new LinkSpec("m", "t"),
            Reference: null,
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.Link, link.Kind);

        // Reference
        MappingRule reference = new MappingRule(
            Name: "r",
            With: new WithBlock(Fhir: "$resource.encounter.reference", OpenEhr: "$reference", Type: WithType.None),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: new ReferenceSpec("Encounter", []),
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.Reference, reference.Kind);

        // SlotArchetypeMarker
        MappingRule marker = new MappingRule(
            Name: "s",
            With: new WithBlock(Fhir: "$resource", OpenEhr: "$composition", Type: WithType.None),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: "COMPOSITION.report.v1.Observation",
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.SlotArchetypeMarker, marker.Kind);

        // Unknown — empty rule with no executable shape.
        MappingRule unknown = new MappingRule(
            Name: "u",
            With: new WithBlock(Fhir: null, OpenEhr: null, Type: WithType.Default),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: null,
            Extension: null,
            FhirCondition: null);
        Assert.Equal(RuleKind.Unknown, unknown.Kind);
    }

    [Fact]
    public void RuleClassifier_VitalStatusBundle_HasNoUnknownRules()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        foreach (ModelMapping model in bundle.Models.Values)
        {
            foreach (MappingRule rule in model.Mappings)
            {
                AssertNotUnknown(rule, model.Metadata.Name);
            }
        }
        foreach (ExtensionMapping ext in bundle.Extensions.Values)
        {
            foreach (MappingRule rule in ext.Mappings)
            {
                AssertNotUnknown(rule, ext.Metadata.Name);
            }
        }

        static void AssertNotUnknown(MappingRule rule, string source)
        {
            AssertRule(rule, source);
            if (rule.FollowedBy is { Mappings.Count: > 0 } fb)
            {
                foreach (MappingRule child in fb.Mappings)
                {
                    AssertNotUnknown(child, source);
                }
            }
            if (rule.Reference is { Mappings.Count: > 0 } refSpec)
            {
                foreach (MappingRule child in refSpec.Mappings)
                {
                    AssertNotUnknown(child, source);
                }
            }
        }

        static void AssertRule(MappingRule rule, string source) =>
            Assert.True(
                rule.Kind != RuleKind.Unknown,
                $"Rule '{rule.Name}' in '{source}' classified as Unknown — classifier is missing a real shape.");
    }

    [Fact]
    public void RuleClassifier_AmbiguousRule_RejectedAtLoad()
    {
        string yaml = """
            grammar: FHIRConnect/v1.0.0
            type: model
            metadata:
              name: ambiguous.test
              version: 0.0.1
            spec:
              system: FHIR
              version: R4
              openEhrConfig:
                archetype: openEHR-EHR-EVALUATION.synth.v1
              fhirConfig:
                structureDefinition: http://hl7.org/fhir/StructureDefinition/Observation
            mappings:
              - name: ambiguousLinkPlusReference
                with:
                  fhir: "$resource.encounter.reference"
                  openehr: "$reference"
                link:
                  meaning: "x"
                  type: "y"
                reference:
                  resourceType: "Encounter"
                  mappings: []
            """;
        string tempFile = Path.Combine(Path.GetTempPath(), $"ambiguous-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, yaml);
        try
        {
            FhirConnectFormatException ex = Assert.Throws<FhirConnectFormatException>(
                () => FhirConnectMapping.Load(tempFile));
            Assert.Contains("link", ex.Message, StringComparison.Ordinal);
            Assert.Contains("reference", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RuleClassifier_PartOfReferenceRules_LoadCleanly()
    {
        // The five top-level partOfReference rules in
        // tests/fixtures/vital-status/model/vital_status.v1.yml carry
        // both `link` AND `with.openehr` / `with.fhir`. with.openehr
        // and with.fhir are NOT primary slots, so the ambiguity
        // throw must not fire for them.
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];
        int partOfRefCount = model.Mappings.Count(r => r.Name == "partOfReference");
        Assert.Equal(5, partOfRefCount);
        foreach (MappingRule rule in model.Mappings.Where(r => r.Name == "partOfReference"))
        {
            Assert.Equal(RuleKind.Link, rule.Kind);
        }
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
