using System;
using System.Diagnostics.CodeAnalysis;
using DotnetOpenEhr.Aql;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Resolves a FHIRconnect-style openEHR path (e.g.
/// <c>$composition/composer</c>, <c>$archetype/data[at0001]/items[at0006]</c>,
/// <c>$openEHRRoot/performer</c>) against a <see cref="BindingContext"/>.
/// Delegates to <see cref="ArchetypePathResolver"/> for the segment
/// past the prefix.
/// </summary>
/// <remarks>
/// Element values are <see cref="Element.Value"/>-unwrapped before
/// being returned — path expressions in FHIRconnect mappings end at
/// the data carrier, not the wrapper. <c>ResolveMany</c> exists for
/// the <c>followedBy</c> + collection case (e.g. iterating each
/// <c>Participation</c>).
/// </remarks>
internal static class OpenEhrPathResolver
{
    /// <summary>
    /// Resolve to (at most) one value. Returns the strongly-typed
    /// underlying data when the resolved node is an
    /// <see cref="Element"/>.
    /// </summary>
    /// <returns><c>null</c> when the path does not resolve.</returns>
    [RequiresUnreferencedCode(
        "Routes through ArchetypePathResolver which walks the typed RM. "
        + "Trim-safe on its own but consumers carry the AOT taint per the "
        + "library-wide v0.x posture.")]
    public static object? Resolve(BindingContext ctx, string path)
    {
        (object? root, string remainder) = NormalizePath(ctx, path);
        if (root is null)
        {
            return null;
        }
        if (string.IsNullOrEmpty(remainder))
        {
            return Unwrap(root);
        }

        if (root is Pathable pathableRoot)
        {
            object? result = ArchetypePathResolver.Resolve(pathableRoot, remainder);
            if (result is null && pathableRoot is Locatable loc)
            {
                // Workaround: DotnetOpenEhr.Aql's PathNavigator only
                // surfaces `links` (and a handful of other generic
                // Locatable attributes) for Composition / Section /
                // the generic Locatable fallback — not for Entry
                // subtypes like Evaluation. Fall back to direct
                // property access for the keys we know the SDK misses.
                result = ResolveLocatableFallback(loc, remainder);
            }
            return Unwrap(result);
        }
        return Unwrap(ResolveOnNonPathable(root, remainder));
    }

    /// <summary>
    /// Resolve to zero or more values. Use when an enclosing rule has
    /// <c>type: NONE + followedBy</c> over a collection
    /// (<c>$composition/context/participations</c>,
    /// <c>$archetype/other_participations</c>).
    /// </summary>
    [RequiresUnreferencedCode("See Resolve.")]
    public static System.Collections.Generic.IEnumerable<object?> ResolveMany(
        BindingContext ctx, string path)
    {
        object? value = Resolve(ctx, path);
        if (value is null)
        {
            yield break;
        }
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            foreach (object? item in enumerable)
            {
                yield return Unwrap(item);
            }
            yield break;
        }
        yield return value;
    }

    /// <summary>
    /// Strip the FHIRconnect prefix and return the appropriate
    /// resolution root plus the leading-slash remainder that
    /// <see cref="ArchetypePathResolver"/> expects.
    /// </summary>
    private static (object? Root, string Remainder) NormalizePath(BindingContext ctx, string path)
    {
        string p = path.Trim();

        if (p.StartsWith("$composition", StringComparison.Ordinal))
        {
            return (ctx.Composition, RemainderAfter(p, "$composition"));
        }
        if (p.StartsWith("$archetype", StringComparison.Ordinal))
        {
            return (ctx.Archetype, RemainderAfter(p, "$archetype"));
        }
        if (p.StartsWith("$openEHRRoot", StringComparison.Ordinal))
        {
            return (ctx.OpenEhrRoot, RemainderAfter(p, "$openEHRRoot"));
        }
        // Bare path (no $-prefix) — resolve against the current
        // openEHR root. Manual fields and the few rare prefix-less
        // openEHR paths land here.
        return (ctx.OpenEhrRoot, EnsureSlash(p));
    }

    /// <summary>
    /// Fallback access for generic <see cref="Locatable"/> attributes
    /// the SDK's <c>PathNavigator</c> doesn't expose on Entry
    /// subtypes (notably <c>links</c> on Evaluation/Observation/etc.).
    /// </summary>
    private static object? ResolveLocatableFallback(Locatable loc, string remainder)
    {
        string seg = remainder.StartsWith('/') ? remainder.Substring(1) : remainder;
        return seg switch
        {
            "links" => loc.Links,
            "name" => loc.Name,
            "uid" => loc.Uid,
            "archetype_node_id" => loc.ArchetypeNodeId,
            "archetype_details" => loc.ArchetypeDetails,
            "feeder_audit" => loc.FeederAudit,
            _ => null,
        };
    }

    /// <summary>
    /// Walk a path on a non-<see cref="Pathable"/> RM object. v0.x
    /// only handles <see cref="DotnetOpenEhr.Rm.Common.Participation"/>
    /// (the followedBy targets inside <c>participations</c>); other
    /// non-Pathable roots return null. Extend the switch as more
    /// rule kinds need it.
    /// </summary>
    private static object? ResolveOnNonPathable(object root, string remainder)
    {
        // Strip leading slash for cleaner segment matching.
        string seg = remainder.StartsWith('/') ? remainder.Substring(1) : remainder;

        if (root is DotnetOpenEhr.Rm.Common.Participation participation)
        {
            return seg switch
            {
                "performer" => participation.Performer,
                "function" => participation.Function,
                "mode" => participation.Mode,
                _ => null,
            };
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

    private static object? Unwrap(object? value)
    {
        // Element wraps a DataValue (DvText, DvCodedText, DvDateTime, ...).
        // FHIRconnect rules in practice expect the unwrapped value.
        if (value is Element element)
        {
            return element.Value;
        }
        return value;
    }
}
