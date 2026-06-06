using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using Microsoft.Extensions.Logging;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Inverse of <see cref="OpenEhrPathResolver"/>: given a typed
/// <see cref="Composition"/>, a FHIRconnect-style openEHR path
/// (<c>$archetype/data[at0001]/items[at0006]</c>,
/// <c>$composition/composer</c>,
/// <c>$archetype/protocol[at0002]/items[at0018]</c>), and an RM
/// value, walks the typed graph and assigns the leaf.
/// </summary>
/// <remarks>
/// <para>
/// Despite the generic name, this writer is intentionally
/// <strong>not</strong> generic in v0.x. There is no SDK factory for
/// "give me an <see cref="Element"/> shaped for archetype path X"
/// today; the writer therefore carries a per-at-code DV factory
/// table for the <c>vital_status</c> model
/// (<c>at0006</c> → <see cref="DvCodedText"/>,
/// <c>at0013</c> → <see cref="DvText"/>,
/// <c>at0018</c> → <see cref="DvDateTime"/>).
/// </para>
/// <para>
/// A future slot replaces the table with an OPT-driven generator.
/// For paths outside the per-at-code table the writer returns
/// <c>ok=false</c> with a clear "not yet supported in v0.x"
/// diagnostic — it never silently no-ops.
/// </para>
/// </remarks>
internal static class OpenEhrPathWriter
{
    private static readonly Regex s_dataItemRegex = new Regex(
        @"^/data\[(?<root>at\w+)\]/items\[(?<leaf>at\w+)\]$",
        RegexOptions.Compiled);

    private static readonly Regex s_protocolItemRegex = new Regex(
        @"^/protocol\[(?<root>at\w+)\]/items\[(?<leaf>at\w+)\]$",
        RegexOptions.Compiled);

    /// <summary>
    /// Per-at-code element-name table for the vital_status model.
    /// Names match the canonical Composition fixture so round-trip
    /// reads/writes do not perturb the Element.Name string the
    /// resolver does not depend on.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> s_elementNameByAtCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["at0006"] = "Vital status",
            ["at0013"] = "Note",
            ["at0018"] = "Effective",
        };

    /// <summary>
    /// Write <paramref name="value"/> at <paramref name="path"/>
    /// against <paramref name="composition"/>. Returns
    /// <c>(true, null)</c> on success or <c>(false, error)</c> when
    /// the path is outside the vital_status scope envelope. When the
    /// writer refuses a path it emits a single
    /// <see cref="LogLevel.Debug"/> entry on
    /// <paramref name="logger"/> (if supplied) so the caller can
    /// trace silent ToOpenEhr refusals without instrumenting every
    /// rule.
    /// </summary>
    public static (bool Ok, string? Error) Write(
        Composition composition,
        string path,
        object? value,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(path);

        (bool ok, string? err) = WriteCore(composition, path, value);
        if (!ok && logger is not null)
        {
            logger.LogDebug("OpenEhrPathWriter refused path '{Path}': {Error}", path, err);
        }
        return (ok, err);
    }

    private static (bool Ok, string? Error) WriteCore(
        Composition composition,
        string path,
        object? value)
    {
        (object? root, string remainder, string prefix) = NormalizePath(composition, path);
        if (root is null)
        {
            return (false, $"OpenEhrPathWriter: path prefix '{prefix}' does not resolve against the composition.");
        }

        if (string.IsNullOrEmpty(remainder))
        {
            return (false, $"OpenEhrPathWriter: empty remainder after prefix '{prefix}' is not assignable.");
        }

        // Archetype-rooted paths target the start Evaluation entry.
        if (string.Equals(prefix, "$archetype", StringComparison.Ordinal) ||
            string.Equals(prefix, "$openEHRRoot", StringComparison.Ordinal))
        {
            if (root is not Evaluation eval)
            {
                return (false,
                    $"OpenEhrPathWriter: '{prefix}' resolves to {root.GetType().Name}; v0.x only writes against an Evaluation start entry.");
            }
            return WriteOnArchetype(eval, remainder, value);
        }

        // Composition-rooted paths target the Composition itself.
        if (string.Equals(prefix, "$composition", StringComparison.Ordinal))
        {
            return WriteOnComposition(composition, remainder, value);
        }

        return (false, $"OpenEhrPathWriter: unrecognized path prefix '{prefix}'.");
    }

    private static (bool Ok, string? Error) WriteOnArchetype(
        Evaluation evaluation,
        string remainder,
        object? value)
    {
        // /data[at0001]/items[atNNNN]
        Match dataMatch = s_dataItemRegex.Match(remainder);
        if (dataMatch.Success)
        {
            string rootAt = dataMatch.Groups["root"].Value;
            string leafAt = dataMatch.Groups["leaf"].Value;
            return WriteIntoItemTree(
                getCurrent: () => evaluation.Data as ItemTree,
                setCurrent: tree => evaluation.Data = tree,
                rootAtCode: rootAt,
                leafAtCode: leafAt,
                value: value);
        }

        // /protocol[at0002]/items[atNNNN]
        Match protoMatch = s_protocolItemRegex.Match(remainder);
        if (protoMatch.Success)
        {
            string rootAt = protoMatch.Groups["root"].Value;
            string leafAt = protoMatch.Groups["leaf"].Value;
            return WriteIntoItemTree(
                getCurrent: () => evaluation.Protocol as ItemTree,
                setCurrent: tree => evaluation.Protocol = tree,
                rootAtCode: rootAt,
                leafAtCode: leafAt,
                value: value);
        }

        // /links — append a Link target to evaluation.Links.
        if (string.Equals(remainder, "/links", StringComparison.Ordinal))
        {
            if (value is not Link link)
            {
                return (false, "OpenEhrPathWriter: /links assignment expects a Link instance.");
            }
            List<Link> links = evaluation.Links is null
                ? []
                : new List<Link>(evaluation.Links);
            links.Add(link);
            evaluation.Links = links;
            return (true, null);
        }

        // /other_participations — append a Participation.
        if (string.Equals(remainder, "/other_participations", StringComparison.Ordinal))
        {
            if (value is not Participation participation)
            {
                return (false, "OpenEhrPathWriter: /other_participations assignment expects a Participation instance.");
            }
            evaluation.OtherParticipations ??= new List<Participation>();
            evaluation.OtherParticipations.Add(participation);
            return (true, null);
        }

        return (false,
            $"OpenEhrPathWriter: archetype-side path '{remainder}' is not yet supported in v0.x (vital_status scope only).");
    }

    private static (bool Ok, string? Error) WriteOnComposition(
        Composition composition,
        string remainder,
        object? value)
    {
        if (string.Equals(remainder, "/composer", StringComparison.Ordinal))
        {
            if (value is not PartyProxy proxy)
            {
                return (false, "OpenEhrPathWriter: /composer assignment expects a PartyProxy instance.");
            }
            composition.Composer = proxy;
            return (true, null);
        }

        if (string.Equals(remainder, "/context/health_care_facility", StringComparison.Ordinal))
        {
            if (value is not PartyIdentified party)
            {
                return (false, "OpenEhrPathWriter: /context/health_care_facility assignment expects a PartyIdentified instance.");
            }
            if (composition.Context is null)
            {
                return (false, "OpenEhrPathWriter: /context/health_care_facility requires composition.Context to be non-null.");
            }
            composition.Context.HealthCareFacility = party;
            return (true, null);
        }

        if (string.Equals(remainder, "/context/participations", StringComparison.Ordinal))
        {
            if (value is not Participation participation)
            {
                return (false, "OpenEhrPathWriter: /context/participations assignment expects a Participation instance.");
            }
            if (composition.Context is null)
            {
                return (false, "OpenEhrPathWriter: /context/participations requires composition.Context to be non-null.");
            }
            composition.Context.Participations ??= new List<Participation>();
            composition.Context.Participations.Add(participation);
            return (true, null);
        }

        return (false,
            $"OpenEhrPathWriter: composition-side path '{remainder}' is not yet supported in v0.x (vital_status scope only).");
    }

    private static (bool Ok, string? Error) WriteIntoItemTree(
        Func<ItemTree?> getCurrent,
        Action<ItemTree> setCurrent,
        string rootAtCode,
        string leafAtCode,
        object? value)
    {
        if (value is not DotnetOpenEhr.Rm.DataTypes.DataValue dv)
        {
            return (false,
                $"OpenEhrPathWriter: items[{leafAtCode}] assignment expects a DataValue; got {value?.GetType().Name ?? "null"}.");
        }

        ItemTree? tree = getCurrent();
        if (tree is null)
        {
            tree = new ItemTree
            {
                Name = new DvText { Value = "Tree" },
                ArchetypeNodeId = rootAtCode,
                Items = new List<Item>(),
            };
            setCurrent(tree);
        }
        else if (!string.Equals(tree.ArchetypeNodeId, rootAtCode, StringComparison.Ordinal))
        {
            return (false,
                $"OpenEhrPathWriter: expected ItemTree[{rootAtCode}] but found ItemTree[{tree.ArchetypeNodeId}].");
        }

        tree.Items ??= new List<Item>();
        Element? existing = tree.Items.OfType<Element>().FirstOrDefault(
            e => string.Equals(e.ArchetypeNodeId, leafAtCode, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Value = dv;
            return (true, null);
        }

        string elementName = s_elementNameByAtCode.TryGetValue(leafAtCode, out string? n)
            ? n
            : leafAtCode;
        Element fresh = new Element
        {
            Name = new DvText { Value = elementName },
            ArchetypeNodeId = leafAtCode,
            Value = dv,
        };
        tree.Items.Add(fresh);
        return (true, null);
    }

    /// <summary>
    /// Strip the FHIRconnect prefix; return the resolution root, the
    /// leading-slash remainder, and the prefix string (for diagnostics).
    /// </summary>
    private static (object? Root, string Remainder, string Prefix) NormalizePath(
        Composition composition,
        string path)
    {
        string p = path.Trim();

        if (p.StartsWith("$composition", StringComparison.Ordinal))
        {
            return (composition, RemainderAfter(p, "$composition"), "$composition");
        }
        if (p.StartsWith("$archetype", StringComparison.Ordinal))
        {
            return (FindStartArchetype(composition), RemainderAfter(p, "$archetype"), "$archetype");
        }
        if (p.StartsWith("$openEHRRoot", StringComparison.Ordinal))
        {
            return (FindStartArchetype(composition), RemainderAfter(p, "$openEHRRoot"), "$openEHRRoot");
        }
        // Bare path — treat as archetype-relative (mirrors the resolver).
        return (FindStartArchetype(composition), EnsureSlash(p), "$archetype");
    }

    private static Evaluation? FindStartArchetype(Composition composition)
    {
        if (composition.Content is null)
        {
            return null;
        }
        foreach (object item in composition.Content)
        {
            if (item is Evaluation eval)
            {
                return eval;
            }
        }
        return null;
    }

    private static string RemainderAfter(string path, string prefix)
    {
        string rest = path.Substring(prefix.Length);
        return EnsureSlash(rest);
    }

    private static string EnsureSlash(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }
        return s.StartsWith('/') ? s : "/" + s;
    }
}
