using System;
using System.Collections.Generic;
using System.Linq;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Mappings;
using DotnetFhirConnect.Tests.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Phase 6 — verify the <see cref="EffectiveMapping.Build"/> merge
/// layer's transitive binding, composite-key matching, and verb
/// dispatch (Add / Overwrite / Remove × 0/1/N matches).
/// </summary>
public sealed class EffectiveMappingTests
{
    [Fact]
    public void BindsExtensionByStartModelName()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];

        EffectiveMapping eff = EffectiveMapping.Build(
            model,
            new[] { bundle.Extensions["KDS_vital_status"] });

        // category + code extension rules are folded in by name binding.
        Assert.Contains(eff.Rules, r => r.Name == "category");
        Assert.Contains(eff.Rules, r => r.Name == "code");
    }

    [Fact]
    public void BindsExtensionTransitivelyViaSlotArchetype()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];

        EffectiveMapping eff = EffectiveMapping.Build(
            model,
            new[]
            {
                bundle.Extensions["KDS_vital_status"],
                bundle.Extensions["KDS_composition"],
            });

        // KDS_composition extends COMPOSITION.report.v1.Observation —
        // bound transitively via KDS_vital_status's compositionMapping
        // slotArchetype.
        Assert.Contains(eff.Rules, r => r.Name == "encounter");
        Assert.Contains(eff.MergeWarnings,
            w => w.Kind == MergeWarningKind.TransitiveBinding && w.RuleName == "KDS_composition");
    }

    [Fact]
    public void AllFivePartOfReferenceRulesSurviveMerge()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];

        EffectiveMapping eff = EffectiveMapping.Build(model, bundle.Extensions.Values);

        List<MappingRule> partOfRefs = eff.Rules.Where(r => r.Name == "partOfReference").ToList();
        Assert.Equal(5, partOfRefs.Count);

        // Pin all five composite keys are present.
        HashSet<(string fhir, string linkType)> keys = partOfRefs
            .Select(r => (r.With.Fhir ?? "", r.Link?.Type ?? ""))
            .ToHashSet();
        Assert.Contains(("$resource.partOf", "partOf"), keys);
        Assert.Contains(("$resource.basedOn", "basedOn"), keys);
        Assert.Contains(("$resource.basedOn", "focus"), keys);
        Assert.Contains(("$resource.encounter", "case"), keys);
        Assert.Contains(("$resource.hasMember", "hasMember"), keys);
    }

    [Fact]
    public void AddWithNoMatch_AppendsRule()
    {
        ModelMapping model = SyntheticModel("model_a", "EVALUATION.synth.v1", []);
        ExtensionMapping ext = SyntheticExtension("ext_a", extends: "model_a",
            rules: [SimpleRule("brand_new", "$resource.brand_new", ExtensionAction.Add)]);

        EffectiveMapping eff = EffectiveMapping.Build(model, [ext]);
        MappingRule appended = Assert.Single(eff.Rules);
        Assert.Equal("brand_new", appended.Name);
    }

    [Fact]
    public void OverwriteWithNoMatch_Throws()
    {
        ModelMapping model = SyntheticModel("m", "EVALUATION.synth.v1", []);
        ExtensionMapping ext = SyntheticExtension("e", extends: "m",
            rules: [SimpleRule("nope", "$resource.nope", ExtensionAction.Overwrite)]);

        FhirConnectFormatException ex = Assert.Throws<FhirConnectFormatException>(
            () => EffectiveMapping.Build(model, [ext]));
        Assert.Contains("overwrite", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void RemoveWithNoMatch_Throws()
    {
        ModelMapping model = SyntheticModel("m", "EVALUATION.synth.v1", []);
        ExtensionMapping ext = SyntheticExtension("e", extends: "m",
            rules: [SimpleRule("missing", "$resource.missing", ExtensionAction.Remove)]);

        FhirConnectFormatException ex = Assert.Throws<FhirConnectFormatException>(
            () => EffectiveMapping.Build(model, [ext]));
        Assert.Contains("remove", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverwriteWithMultipleMatches_Throws()
    {
        ModelMapping model = SyntheticModel("m", "EVALUATION.synth.v1",
        [
            SimpleRule("dup", "$resource.dup", extension: null),
            SimpleRule("dup", "$resource.dup", extension: null),
        ]);
        ExtensionMapping ext = SyntheticExtension("e", extends: "m",
            rules: [SimpleRule("dup", "$resource.dup", ExtensionAction.Overwrite)]);

        FhirConnectFormatException ex = Assert.Throws<FhirConnectFormatException>(
            () => EffectiveMapping.Build(model, [ext]));
        Assert.Contains("overwrite", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RemoveWithMultipleMatches_Throws()
    {
        ModelMapping model = SyntheticModel("m", "EVALUATION.synth.v1",
        [
            SimpleRule("dup", "$resource.dup", extension: null),
            SimpleRule("dup", "$resource.dup", extension: null),
        ]);
        ExtensionMapping ext = SyntheticExtension("e", extends: "m",
            rules: [SimpleRule("dup", "$resource.dup", ExtensionAction.Remove)]);

        Assert.Throws<FhirConnectFormatException>(
            () => EffectiveMapping.Build(model, [ext]));
    }

    [Fact]
    public void MergeWarnings_PopulatedForTransitiveBindings()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];

        EffectiveMapping eff = EffectiveMapping.Build(model, bundle.Extensions.Values);
        Assert.Contains(eff.MergeWarnings,
            w => w.Kind == MergeWarningKind.TransitiveBinding && w.RuleName == "KDS_composition");
    }

    [Fact]
    public void LoggerSeam_EmitsInformationOnTransitiveBinding()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        ModelMapping model = bundle.Models["EVALUATION.vital_status.v1"];

        CapturingLogger captured = new CapturingLogger();
        _ = EffectiveMapping.Build(model, bundle.Extensions.Values, captured);

        Assert.Contains(captured.Entries,
            e => e.Level == LogLevel.Information &&
                 e.Message.Contains("KDS_composition", StringComparison.Ordinal) &&
                 e.Message.Contains("transitively", StringComparison.OrdinalIgnoreCase));
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

    private static ExtensionMapping SyntheticExtension(string name, string extends, IReadOnlyList<MappingRule> rules) =>
        new ExtensionMapping(
            grammar: FhirConnectGrammar.V1_0_0,
            metadata: new MappingMetadata(name, "0.0.1"),
            spec: new MappingSpec(
                System: "FHIR",
                Version: FhirRelease.R4,
                Extends: extends,
                OpenEhrConfig: null,
                FhirConfig: null),
            mappings: rules);

    private static MappingRule SimpleRule(string name, string fhir, ExtensionAction? extension) =>
        new MappingRule(
            Name: name,
            With: new WithBlock(Fhir: fhir, OpenEhr: null, Type: WithType.Default),
            Unidirectional: null,
            Manual: null,
            FollowedBy: null,
            Link: null,
            Reference: null,
            SlotArchetype: null,
            Extension: extension,
            FhirCondition: null);

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
