using System;
using System.Linq;
using DotnetFhirConnect;
using DotnetFhirConnect.Engine;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using OpenEhrEvaluation = DotnetOpenEhr.Rm.Composition.Evaluation;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 3 — round-trip Composition → Observation → Composition
/// against the canonical vital_status fixture. Pinned assertion set
/// is the three bidirectional leaves only (effective / vitalStatus /
/// note); link-driven and performer-collapse fields are documented
/// as direction-asymmetric in v0.x and excluded.
/// </summary>
public sealed class RoundTripTests
{
    [Fact]
    public void VitalStatus_FieldEquivalentOnBidirectionalFields()
    {
        MappingBundle bundle = EngineFixtures.LoadBundle();
        OpenEhrComposition canonical = EngineFixtures.LoadComposition();

        // First hop: Composition → Observation.
        R4Engine r4 = new R4Engine(bundle);
        Resource produced = r4.ToFhir(canonical);
        Observation obs = Assert.IsType<Observation>(produced);

        // Build the "blanked skeleton" — same shape as the canonical
        // composition but with the three round-tripped leaves
        // stripped. We reload from disk to keep the canonical instance
        // untouched for the assertion.
        OpenEhrComposition skeleton = EngineFixtures.LoadComposition();
        BlankRoundTripLeaves(skeleton);

        // Second hop: Observation → Composition via the internal overload.
        OpenEhrComposition rebuilt = InvokeInternalToOpenEhr(r4.Core, obs, skeleton);
        Assert.Same(skeleton, rebuilt);

        // Re-read the three leaves via the resolver and assert equality
        // with the canonical fixture using the pinned comparison
        // semantics (rubber-duck issue #8).
        BindingContext canonicalCtx = MakeContext(canonical);
        BindingContext rebuiltCtx = MakeContext(rebuilt);

        AssertEffectiveEqual(canonicalCtx, rebuiltCtx);
        AssertVitalStatusEqual(canonicalCtx, rebuiltCtx);
        AssertNoteEqual(canonicalCtx, rebuiltCtx);
    }

    private static void AssertEffectiveEqual(BindingContext expected, BindingContext actual)
    {
        const string path = "$archetype/protocol[at0002]/items[at0018]";
        DvDateTime expectedDt = Assert.IsType<DvDateTime>(OpenEhrPathResolver.Resolve(expected, path));
        DvDateTime actualDt = Assert.IsType<DvDateTime>(OpenEhrPathResolver.Resolve(actual, path));
        DateTimeOffset expectedDto = DateTimeOffset.Parse(expectedDt.Value.ToString());
        DateTimeOffset actualDto = DateTimeOffset.Parse(actualDt.Value.ToString());
        Assert.Equal(expectedDto, actualDto);
    }

    private static void AssertVitalStatusEqual(BindingContext expected, BindingContext actual)
    {
        const string path = "$archetype/data[at0001]/items[at0006]";
        DvCodedText expectedC = Assert.IsType<DvCodedText>(OpenEhrPathResolver.Resolve(expected, path));
        DvCodedText actualC = Assert.IsType<DvCodedText>(OpenEhrPathResolver.Resolve(actual, path));
        Assert.Equal(expectedC.DefiningCode.TerminologyId.Value, actualC.DefiningCode.TerminologyId.Value);
        Assert.Equal(expectedC.DefiningCode.CodeString, actualC.DefiningCode.CodeString);
        Assert.Equal(expectedC.Value, actualC.Value);
    }

    private static void AssertNoteEqual(BindingContext expected, BindingContext actual)
    {
        const string path = "$archetype/data[at0001]/items[at0013]";
        DvText expectedT = Assert.IsType<DvText>(OpenEhrPathResolver.Resolve(expected, path));
        DvText actualT = Assert.IsType<DvText>(OpenEhrPathResolver.Resolve(actual, path));
        Assert.Equal(expectedT.Value, actualT.Value);
    }

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
        throw new InvalidOperationException("Test fixture is missing an Evaluation entry.");
    }

    private static void BlankRoundTripLeaves(OpenEhrComposition skeleton)
    {
        OpenEhrEvaluation eval = FindEvaluation(skeleton);
        RemoveLeaf(eval.Data as ItemTree, "at0006");
        RemoveLeaf(eval.Data as ItemTree, "at0013");
        RemoveLeaf(eval.Protocol as ItemTree, "at0018");
    }

    private static void RemoveLeaf(ItemTree? tree, string atCode)
    {
        if (tree?.Items is null)
        {
            return;
        }
        for (int i = tree.Items.Count - 1; i >= 0; i--)
        {
            if (tree.Items[i] is DotnetOpenEhr.Rm.DataStructures.Element e &&
                string.Equals(e.ArchetypeNodeId, atCode, StringComparison.Ordinal))
            {
                tree.Items.RemoveAt(i);
            }
        }
    }

    private static OpenEhrComposition InvokeInternalToOpenEhr(
        FhirConnectEngine core, object resource, OpenEhrComposition skeleton)
    {
        // The (object, Composition) overload is internal-visible to
        // this test assembly via InternalsVisibleTo, but the public
        // single-arg overload throws — disambiguate at the call site.
        return core.ToOpenEhr(resource, skeleton);
    }
}
