using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Rm.Common;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Walks a model mapping's rule list against a
/// <see cref="BindingContext"/>, dispatching on rule kind and
/// pushing values into the FHIR side via the supplied
/// <see cref="IFhirAdapter"/>. v0.x covers the model-rule kinds
/// listed in Phase 6a of the plan; extension-rule kinds
/// (<c>reference</c>, <c>slotArchetype</c>,
/// <c>extension: add/overwrite/remove</c>) are deferred to Phase 6b.
/// </summary>
internal sealed class MappingRuleExecutor
{
    private readonly IFhirAdapter _adapter;
    private readonly TransformDirection _direction;

    public MappingRuleExecutor(IFhirAdapter adapter, TransformDirection direction)
    {
        _adapter = adapter;
        _direction = direction;
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public void ExecuteAll(BindingContext rootCtx, IReadOnlyList<MappingRule> rules)
    {
        foreach (MappingRule rule in rules)
        {
            Execute(rootCtx, rule);
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public void Execute(BindingContext ctx, MappingRule rule)
    {
        if (IsUnidirectionalAgainstUs(rule))
        {
            return;
        }
        if (rule.Extension is not null && rule.Reference is null && rule.SlotArchetype is null)
        {
            // Phase 6a: extension verbs on model rules are no-ops in v0.x.
            // The extension-only rule kinds (reference / slotArchetype) are
            // entirely deferred to Phase 6b.
        }
        if (rule.Reference is not null || rule.SlotArchetype is not null)
        {
            // Phase 6b territory; ignore.
            return;
        }

        if (rule.FollowedBy is { Mappings.Count: > 0 } fb &&
            rule.With.Type == WithType.None)
        {
            ExecuteWrapperFollowedBy(ctx, rule, fb);
            return;
        }

        if (rule.Link is not null)
        {
            ExecuteLink(ctx, rule);
            return;
        }

        if (rule.Manual is { Count: > 0 } manualEntries)
        {
            ExecuteManual(ctx, rule, manualEntries);
            return;
        }

        if (rule.With.OpenEhr is not null && rule.With.Fhir is not null)
        {
            if (_direction == TransformDirection.ToFhir)
            {
                ExecuteDirectToFhir(ctx, rule);
            }
            else
            {
                ExecuteDirectToOpenEhr(ctx, rule);
            }
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteWrapperFollowedBy(BindingContext ctx, MappingRule rule, FollowedBy fb)
    {
        // Wrapper rule (type:NONE). Resolve the openEHR sub-root
        // (typically a collection like participations) and iterate.
        if (rule.With.OpenEhr is null)
        {
            // No openEHR root specified — just execute children once
            // against the current context.
            foreach (MappingRule child in fb.Mappings)
            {
                Execute(ctx, child);
            }
            return;
        }

        foreach (object? item in OpenEhrPathResolver.ResolveMany(ctx, rule.With.OpenEhr))
        {
            if (item is null)
            {
                continue;
            }
            BindingContext nested = ctx.PushFollowedBy(item, rule.With.Fhir);
            foreach (MappingRule child in fb.Mappings)
            {
                Execute(nested, child);
            }
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteDirectToFhir(BindingContext ctx, MappingRule rule)
    {
        object? value = OpenEhrPathResolver.Resolve(ctx, rule.With.OpenEhr!);
        if (value is null)
        {
            return;
        }
        object? translated = OpenEhrToFhirTranslator.Translate(value);
        string fhirPath = ResolveFhirPath(ctx, rule.With.Fhir!);
        if (!_adapter.TrySetValue(ctx.Resource, fhirPath, translated, out string? error))
        {
            throw new InvalidOperationException(
                $"FhirConnectEngine: rule '{rule.Name}' failed to assign FHIR path '{fhirPath}' (raw '{rule.With.Fhir}'): {error}");
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteDirectToOpenEhr(BindingContext ctx, MappingRule rule)
    {
        string fhirPath = ResolveFhirPath(ctx, rule.With.Fhir!);
        if (!_adapter.TryGetValue(ctx.Resource, fhirPath, out object? fhirValue) || fhirValue is null)
        {
            return;
        }
        object? translated = FhirToOpenEhrTranslator.Translate(fhirValue);
        if (translated is null)
        {
            return;
        }
        (bool ok, string? err) = OpenEhrPathWriter.Write(ctx.Composition, rule.With.OpenEhr!, translated);
        if (!ok)
        {
            // Direction-asymmetric paths (link-driven, performer collapse,
            // etc.) are documented as ToFhir-only in v0.x; skip cleanly
            // rather than throw so a single asymmetric rule does not
            // poison the rest of the model walk.
            _ = err;
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteLink(BindingContext ctx, MappingRule rule)
    {
        if (_direction != TransformDirection.ToFhir)
        {
            return;
        }

        // The openEHR-side path is `$archetype/links`. Iterate every
        // match (use ResolveMany — Resolve throws on >1 hit) and
        // filter on rule.link.type. Each kept Link's Target becomes
        // a FHIR ResourceReference written to the rule's fhir path.
        foreach (object? candidate in OpenEhrPathResolver.ResolveMany(ctx, rule.With.OpenEhr ?? "$archetype/links"))
        {
            if (candidate is not Link link)
            {
                continue;
            }
            if (rule.Link is not null &&
                link.Type is not null &&
                !string.Equals(link.Type.Value, rule.Link.Type, StringComparison.Ordinal))
            {
                continue;
            }

            object? translated = OpenEhrToFhirTranslator.Translate(link.Target);
            string fhirPath = ResolveFhirPath(ctx, rule.With.Fhir!);
            if (!_adapter.TrySetValue(ctx.Resource, fhirPath, translated, out string? error))
            {
                throw new InvalidOperationException(
                    $"FhirConnectEngine: link rule '{rule.Name}' failed to assign FHIR path '{fhirPath}': {error}");
            }
        }
    }

    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    private void ExecuteManual(BindingContext ctx, MappingRule rule, IReadOnlyList<ManualEntry> entries)
    {
        if (_direction == TransformDirection.ToFhir)
        {
            foreach (ManualEntry entry in entries)
            {
                if (entry.Fhir is null)
                {
                    continue;
                }
                foreach (ManualField field in entry.Fhir)
                {
                    string fhirPath = CombineFhirPath(ctx.FhirRoot, field.Path);
                    if (!_adapter.TrySetValue(ctx.Resource, fhirPath, field.Value, out string? error))
                    {
                        // Manual paths target nested coding fields we
                        // don't currently model in R4Adapter (e.g.
                        // "coding.code"). Tolerate: this is Phase 6b
                        // territory once the adapter grows nested
                        // codeable-concept setters.
                        _ = error;
                    }
                }
            }
            return;
        }

        // ToOpenEhr: write manual openEHR-side constants back into the
        // typed Composition graph. The vital_status fixture exercises
        // this via `participationFunction.manual.openehr.function = "performer"`.
        foreach (ManualEntry entry in entries)
        {
            if (entry.OpenEhr is null)
            {
                continue;
            }
            foreach (ManualField field in entry.OpenEhr)
            {
                // Manual openEHR constants are emitted as plain strings;
                // pre-wrap them in DvText for the writer. Targets that
                // expect a different RM type will be caught by the
                // writer's per-path arm.
                DotnetOpenEhr.Rm.DataTypes.Text.DvText boxed =
                    new DotnetOpenEhr.Rm.DataTypes.Text.DvText { Value = field.Value };
                (bool ok, string? err) = OpenEhrPathWriter.Write(ctx.Composition, field.Path, boxed);
                if (!ok)
                {
                    // Same tolerance as the ToFhir branch above —
                    // manual writes are best-effort in v0.x.
                    _ = err;
                }
            }
        }
    }

    private static string CombineFhirPath(string root, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return root;
        }
        if (path.StartsWith('$'))
        {
            return path;
        }
        return root.EndsWith('.') ? root + path : root + "." + path;
    }

    /// <summary>
    /// Resolve a rule's <c>with.fhir</c> path to the absolute path
    /// the <see cref="IFhirAdapter"/> expects: <c>$fhirRoot</c> /
    /// <c>$fhirRoot.X</c> get substituted with the enclosing
    /// <see cref="BindingContext.FhirRoot"/>; bare paths / explicit
    /// <c>$resource</c> paths pass through.
    /// </summary>
    private static string ResolveFhirPath(BindingContext ctx, string fhirPath)
    {
        if (string.IsNullOrEmpty(fhirPath))
        {
            return ctx.FhirRoot;
        }
        if (fhirPath.StartsWith("$fhirRoot", StringComparison.Ordinal))
        {
            string remainder = fhirPath.Substring("$fhirRoot".Length);
            if (string.IsNullOrEmpty(remainder))
            {
                return ctx.FhirRoot;
            }
            return ctx.FhirRoot + remainder;
        }
        return fhirPath;
    }

    private bool IsUnidirectionalAgainstUs(MappingRule rule)
    {
        if (rule.Unidirectional is null)
        {
            return false;
        }
        // unidirectional value is the *allowed* direction; if it
        // doesn't match ours, skip the rule.
        return _direction switch
        {
            TransformDirection.ToFhir => !string.Equals(rule.Unidirectional, "to_fhir", StringComparison.Ordinal),
            TransformDirection.ToOpenEhr => !string.Equals(rule.Unidirectional, "to_openehr", StringComparison.Ordinal),
            _ => false,
        };
    }
}
