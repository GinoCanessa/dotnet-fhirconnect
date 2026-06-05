extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using System;
using DotnetFhirConnect;
using DotnetFhirConnect.Engine;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using OpenEhrEvaluation = DotnetOpenEhr.Rm.Composition.Evaluation;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 4 — exercise the public <c>ToOpenEhr(object)</c> overload
/// (skeleton built automatically by <c>SkeletonBuilder</c>) end-to-end,
/// and pin the unknown-archetype failure mode.
/// </summary>
public sealed class ToOpenEhrTests
{
    [Fact]
    public void VitalStatus_Observation_RoundTripsToCompositionWithLeafEquivalence()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition canonical = EngineFixtures.LoadComposition();

        R4Engine r4 = new R4Engine(bundle);
        Resource produced = r4.ToFhir(canonical);
        Observation obs = Assert.IsType<Observation>(produced);

        // Public single-arg overload — skeleton built internally.
        OpenEhrComposition rebuilt = r4.Core.ToOpenEhr(obs);

        DvDateTime canonicalEffective = ReadDvDateTime(
            canonical, "$archetype/protocol[at0002]/items[at0018]");
        DvDateTime rebuiltEffective = ReadDvDateTime(
            rebuilt, "$archetype/protocol[at0002]/items[at0018]");
        DateTimeOffset canonicalDto = DateTimeOffset.Parse(canonicalEffective.Value.ToString());
        DateTimeOffset rebuiltDto = DateTimeOffset.Parse(rebuiltEffective.Value.ToString());
        Assert.Equal(canonicalDto, rebuiltDto);

        DvCodedText canonicalStatus = ReadDvCodedText(
            canonical, "$archetype/data[at0001]/items[at0006]");
        DvCodedText rebuiltStatus = ReadDvCodedText(
            rebuilt, "$archetype/data[at0001]/items[at0006]");
        Assert.Equal(canonicalStatus.DefiningCode.CodeString, rebuiltStatus.DefiningCode.CodeString);
        Assert.Equal(canonicalStatus.DefiningCode.TerminologyId.Value, rebuiltStatus.DefiningCode.TerminologyId.Value);
        Assert.Equal(canonicalStatus.Value, rebuiltStatus.Value);

        DvText canonicalNote = ReadDvText(canonical, "$archetype/data[at0001]/items[at0013]");
        DvText rebuiltNote = ReadDvText(rebuilt, "$archetype/data[at0001]/items[at0013]");
        Assert.Equal(canonicalNote.Value, rebuiltNote.Value);
    }

    [Fact]
    public void ToOpenEhr_UnknownArchetype_ThrowsNotSupported()
    {
        // Build a synthetic model mapping pointing at an archetype the
        // SkeletonBuilder does not know how to bootstrap.
        ModelMapping synthetic = MakeSyntheticModel(
            archetype: "openEHR-EHR-EVALUATION.does_not_exist.v1");
        MappingBundle bundle = new MappingBundle(
            Context: null,
            Models: new Dictionary<string, ModelMapping> { [synthetic.Metadata.Name] = synthetic },
            Extensions: new Dictionary<string, ExtensionMapping>());

        FhirConnectEngine engine = new FhirConnectEngine(bundle);
        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => engine.ToOpenEhr(new Observation()));
        Assert.Contains("vital_status-only", ex.Message);
    }

    private static DvDateTime ReadDvDateTime(OpenEhrComposition comp, string path)
        => Assert.IsType<DvDateTime>(OpenEhrPathResolver.Resolve(MakeContext(comp), path));

    private static DvCodedText ReadDvCodedText(OpenEhrComposition comp, string path)
        => Assert.IsType<DvCodedText>(OpenEhrPathResolver.Resolve(MakeContext(comp), path));

    private static DvText ReadDvText(OpenEhrComposition comp, string path)
        => Assert.IsType<DvText>(OpenEhrPathResolver.Resolve(MakeContext(comp), path));

    private static BindingContext MakeContext(OpenEhrComposition comp)
    {
        OpenEhrEvaluation eval = FindEvaluation(comp);
        return new BindingContext(
            Composition: comp,
            Archetype: eval,
            OpenEhrRoot: eval,
            Resource: new object(),
            FhirRoot: "$resource");
    }

    private static OpenEhrEvaluation FindEvaluation(OpenEhrComposition comp)
    {
        Assert.NotNull(comp.Content);
        foreach (object item in comp.Content!)
        {
            if (item is OpenEhrEvaluation eval)
            {
                return eval;
            }
        }
        throw new InvalidOperationException("Composition has no Evaluation entry.");
    }

    private static ModelMapping MakeSyntheticModel(string archetype) =>
        new ModelMapping(
            grammar: FhirConnectGrammar.V1_0_0,
            metadata: new MappingMetadata("synthetic", "0.0.1"),
            spec: new MappingSpec(
                System: "FHIR",
                Version: FhirRelease.R4,
                Extends: null,
                OpenEhrConfig: new OpenEhrConfig(archetype, Revision: null),
                FhirConfig: new FhirConfig("http://hl7.org/fhir/StructureDefinition/Observation")),
            mappings: []);
}
