using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DotnetFhirConnect.Engine;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Text;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Acceptance tests for the OPT-driven element-name lookup in
/// <see cref="OpenEhrPathWriter"/>. The vendored German
/// <c>KDS_Vitalstatus.opt</c> supplies the names; every test builds its
/// composition from the canonical fixture so the
/// <c>Evaluation.ArchetypeDetails.ArchetypeId.Value</c>
/// (<c>openEHR-EHR-EVALUATION.vital_status.v1</c>) is populated — the
/// lookup is driven entirely by that value.
/// </summary>
public sealed class OpTemplateBackedWriterTests
{
    private const string DataLeafCoded = "$archetype/data[at0001]/items[at0006]";
    private const string DataLeafNote = "$archetype/data[at0001]/items[at0013]";
    private const string ProtocolLeafTime = "$archetype/protocol[at0002]/items[at0018]";

    [Fact]
    [RequiresUnreferencedCode("Loads OPT1.4 XML; not AOT-publishable in v0.x.")]
    public void OptDriven_ElementNames_AreGermanFromTemplate()
    {
        IOperationalTemplate template = EngineFixtures.LoadOperationalTemplate();
        Composition comp = EngineFixtures.LoadComposition();

        AssertGermanName(comp, template, DataLeafCoded, root: "at0001", leaf: "at0006",
            protocol: false, expected: "Vitalstatus");
        AssertGermanName(comp, template, DataLeafNote, root: "at0001", leaf: "at0013",
            protocol: false, expected: "Kommentar");
        AssertGermanName(comp, template, ProtocolLeafTime, root: "at0002", leaf: "at0018",
            protocol: true, expected: "Zeitpunkt der Feststellung");
    }

    [Fact]
    [RequiresUnreferencedCode("Loads OPT1.4 XML; not AOT-publishable in v0.x.")]
    public void OptDriven_LeafValue_RoundTripsUnchanged()
    {
        IOperationalTemplate template = EngineFixtures.LoadOperationalTemplate();
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);

        object? original = OpenEhrPathResolver.Resolve(ctx, DataLeafCoded);
        DvCodedText originalCoded = Assert.IsType<DvCodedText>(original);

        BlankElement(comp, root: "at0001", leaf: "at0006", protocol: false);
        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, DataLeafCoded, originalCoded, template);
        Assert.True(ok, err);

        DvCodedText reread = Assert.IsType<DvCodedText>(OpenEhrPathResolver.Resolve(ctx, DataLeafCoded));
        Assert.Equal(originalCoded.Value, reread.Value);
        Assert.Equal(originalCoded.DefiningCode.CodeString, reread.DefiningCode.CodeString);
        Assert.Equal(
            originalCoded.DefiningCode.TerminologyId.Value,
            reread.DefiningCode.TerminologyId.Value);
        // The OPT path renames the element but must not perturb the value.
        Assert.Equal("Vitalstatus", ReadElementName(comp, root: "at0001", leaf: "at0006", protocol: false));
    }

    [Fact]
    public void NullTemplate_FallsBackToAtCode()
    {
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);

        object? original = OpenEhrPathResolver.Resolve(ctx, DataLeafCoded);
        DvCodedText originalCoded = Assert.IsType<DvCodedText>(original);

        BlankElement(comp, root: "at0001", leaf: "at0006", protocol: false);
        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, DataLeafCoded, originalCoded, template: null);
        Assert.True(ok, err);

        // With no template and the hand-table gone, the bare at-code is used.
        Assert.Equal("at0006", ReadElementName(comp, root: "at0001", leaf: "at0006", protocol: false));
    }

    [Fact]
    [RequiresUnreferencedCode("Loads OPT1.4 XML; not AOT-publishable in v0.x.")]
    public void TemplateId_IsKdsVitalstatus()
    {
        IOperationalTemplate template = EngineFixtures.LoadOperationalTemplate();
        // Passes only because the adapter reads HeaderMetadata["template_id"];
        // the raw SDK TemplateId would return the root concept id "report".
        Assert.Equal("KDS_Vitalstatus", template.TemplateId);
    }

    private static void AssertGermanName(
        Composition comp,
        IOperationalTemplate template,
        string path,
        string root,
        string leaf,
        bool protocol,
        string expected)
    {
        BindingContext ctx = MakeContext(comp);
        object? original = OpenEhrPathResolver.Resolve(ctx, path);
        DotnetOpenEhr.Rm.DataTypes.DataValue dv =
            Assert.IsAssignableFrom<DotnetOpenEhr.Rm.DataTypes.DataValue>(original);

        BlankElement(comp, root: root, leaf: leaf, protocol: protocol);
        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, path, dv, template);
        Assert.True(ok, err);

        Assert.Equal(expected, ReadElementName(comp, root: root, leaf: leaf, protocol: protocol));
    }

    private static string ReadElementName(Composition comp, string root, string leaf, bool protocol)
    {
        Evaluation eval = FindEvaluation(comp);
        ItemTree? tree = protocol ? eval.Protocol as ItemTree : eval.Data as ItemTree;
        Assert.NotNull(tree);
        Assert.Equal(root, tree!.ArchetypeNodeId);
        Assert.NotNull(tree.Items);
        foreach (Item item in tree.Items!)
        {
            if (item is Element e &&
                string.Equals(e.ArchetypeNodeId, leaf, StringComparison.Ordinal))
            {
                DvText name = Assert.IsType<DvText>(e.Name);
                return name.Value;
            }
        }
        throw new InvalidOperationException($"Element {leaf} not found under {root}.");
    }

    private static BindingContext MakeContext(Composition comp)
    {
        Evaluation eval = FindEvaluation(comp);
        return new BindingContext(
            Composition: comp,
            Archetype: eval,
            OpenEhrRoot: eval,
            Resource: new object(),
            FhirRoot: "$resource");
    }

    private static Evaluation FindEvaluation(Composition comp)
    {
        Assert.NotNull(comp.Content);
        foreach (object item in comp.Content!)
        {
            if (item is Evaluation eval)
            {
                return eval;
            }
        }
        throw new InvalidOperationException("Test fixture is missing an Evaluation entry.");
    }

    private static void BlankElement(Composition comp, string root, string leaf, bool protocol)
    {
        Evaluation eval = FindEvaluation(comp);
        ItemTree? tree = protocol ? eval.Protocol as ItemTree : eval.Data as ItemTree;
        Assert.NotNull(tree);
        Assert.Equal(root, tree!.ArchetypeNodeId);
        Assert.NotNull(tree.Items);
        IList<Item> items = tree.Items!;
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is Element e &&
                string.Equals(e.ArchetypeNodeId, leaf, StringComparison.Ordinal))
            {
                items.RemoveAt(i);
            }
        }
    }
}
