using System;
using System.Collections.Generic;
using DotnetFhirConnect.Engine;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 2 — round-trip the model-rule leaf paths via
/// <see cref="OpenEhrPathWriter"/> against the canonical vital_status
/// composition, plus a negative test that an unmodelled path returns
/// <c>ok=false</c> rather than throwing.
/// </summary>
public sealed class OpenEhrPathWriterTests
{
    [Fact]
    public void EffectivePath_BlankAndRewrite_RoundTrips()
    {
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);
        const string path = "$archetype/protocol[at0002]/items[at0018]";

        object? original = OpenEhrPathResolver.Resolve(ctx, path);
        DvDateTime originalDt = Assert.IsType<DvDateTime>(original);

        BlankElement(comp, root: "at0002", leaf: "at0018", protocol: true);

        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, path, originalDt);
        Assert.True(ok, err);

        object? rereadObj = OpenEhrPathResolver.Resolve(ctx, path);
        DvDateTime reread = Assert.IsType<DvDateTime>(rereadObj);
        Assert.Equal(originalDt.Value.ToString(), reread.Value.ToString());
    }

    [Fact]
    public void VitalStatusPath_BlankAndRewrite_RoundTrips()
    {
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);
        const string path = "$archetype/data[at0001]/items[at0006]";

        object? original = OpenEhrPathResolver.Resolve(ctx, path);
        DvCodedText originalCoded = Assert.IsType<DvCodedText>(original);

        BlankElement(comp, root: "at0001", leaf: "at0006", protocol: false);

        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, path, originalCoded);
        Assert.True(ok, err);

        object? rereadObj = OpenEhrPathResolver.Resolve(ctx, path);
        DvCodedText reread = Assert.IsType<DvCodedText>(rereadObj);
        Assert.Equal(originalCoded.Value, reread.Value);
        Assert.Equal(originalCoded.DefiningCode.CodeString, reread.DefiningCode.CodeString);
        Assert.Equal(originalCoded.DefiningCode.TerminologyId.Value, reread.DefiningCode.TerminologyId.Value);
    }

    [Fact]
    public void NotePath_BlankAndRewrite_RoundTrips()
    {
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);
        const string path = "$archetype/data[at0001]/items[at0013]";

        object? original = OpenEhrPathResolver.Resolve(ctx, path);
        DvText originalText = Assert.IsType<DvText>(original);

        BlankElement(comp, root: "at0001", leaf: "at0013", protocol: false);

        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, path, originalText);
        Assert.True(ok, err);

        object? rereadObj = OpenEhrPathResolver.Resolve(ctx, path);
        DvText reread = Assert.IsType<DvText>(rereadObj);
        Assert.Equal(originalText.Value, reread.Value);
    }

    [Fact]
    public void NewLeaf_InEmptyProtocol_IsCreatedFromScratch()
    {
        Composition comp = EngineFixtures.LoadComposition();
        BindingContext ctx = MakeContext(comp);

        // Strip the Protocol entirely; the writer must lazily build it.
        Evaluation eval = FindEvaluation(comp);
        eval.Protocol = null;

        const string path = "$archetype/protocol[at0002]/items[at0018]";
        DvDateTime payload = new DvDateTime
        {
            Value = IsoDateTime.Parse("2020-01-02T03:04:05Z".AsSpan()),
        };

        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, path, payload);
        Assert.True(ok, err);

        object? readBack = OpenEhrPathResolver.Resolve(ctx, path);
        DvDateTime reread = Assert.IsType<DvDateTime>(readBack);
        Assert.Equal(payload.Value.ToString(), reread.Value.ToString());
    }

    [Fact]
    public void UnsupportedPath_ReturnsFalse_DoesNotThrow()
    {
        Composition comp = EngineFixtures.LoadComposition();
        (bool ok, string? err) = OpenEhrPathWriter.Write(
            comp,
            "$archetype/state[at0099]/foo",
            new DvText { Value = "n/a" });
        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("not yet supported", err!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Composer_OnComposition_AssignsPartyProxy()
    {
        Composition comp = EngineFixtures.LoadComposition();
        PartyIdentified newComposer = new PartyIdentified { Name = "Replacement Composer" };
        (bool ok, string? err) = OpenEhrPathWriter.Write(comp, "$composition/composer", newComposer);
        Assert.True(ok, err);
        PartyIdentified actual = Assert.IsType<PartyIdentified>(comp.Composer);
        Assert.Equal("Replacement Composer", actual.Name);
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
