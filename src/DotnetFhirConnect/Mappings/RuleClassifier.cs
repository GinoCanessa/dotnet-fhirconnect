using System.Collections.Generic;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// Parse-time classification of a <see cref="MappingRule"/>'s
/// executable shape. The engine's executor switches on this enum
/// rather than re-pattern-matching populated slots on every
/// dispatch.
/// </summary>
public enum RuleKind
{
    /// <summary>
    /// The rule has no recognised executable shape — preserves the
    /// pre-Phase-4 silent no-op semantics of
    /// <c>MappingRuleExecutor.Execute</c> for inputs that fell off
    /// the bottom of the if-chain.
    /// </summary>
    Unknown,

    /// <summary>
    /// Direct copy: <c>with.openehr</c> and <c>with.fhir</c> are
    /// both set, <c>with.type</c> is not <c>NONE</c>, and there is no
    /// `manual` / `followedBy` / `link` / `reference` block.
    /// </summary>
    DirectCopy,

    /// <summary>
    /// Wrapper rule: <c>followedBy</c> populated and
    /// <c>with.type == NONE</c>. The outer rule establishes a
    /// sub-root; nested rules carry the data.
    /// </summary>
    WrapperFollowedBy,

    /// <summary>
    /// Manual constant assignment: the rule carries one or more
    /// <c>manual</c> entries (FHIR-side, openEHR-side, or both).
    /// </summary>
    Manual,

    /// <summary>
    /// Composition link rule: carries a <c>link</c> block matching
    /// against <c>Composition.Links</c> in the ToFhir direction.
    /// </summary>
    Link,

    /// <summary>
    /// Reference rule: carries a <c>reference</c> block. The rule
    /// builds a <c>ResourceReference</c> and runs nested mappings
    /// against it.
    /// </summary>
    Reference,

    /// <summary>
    /// Informational <c>slotArchetype</c>-only marker:
    /// <c>slotArchetype</c> is set but there is no executable copy
    /// shape (<c>with.type == NONE</c>, no <c>followedBy</c>, no
    /// <c>manual</c>, no <c>link</c>). Surfaced to the merge layer's
    /// transitive bound-name set; the executor treats this as a
    /// no-op.
    /// </summary>
    SlotArchetypeMarker,
}

/// <summary>
/// Parse-time classifier for <see cref="MappingRule"/> shapes.
/// Mirrors the historical dispatch order in
/// <c>MappingRuleExecutor.Execute</c> verbatim so behaviour is
/// preserved across the Phase 4 refactor. Also exposes
/// <see cref="AmbiguousPrimarySlots"/> for the loader-side ambiguity
/// gate.
/// </summary>
public static class RuleClassifier
{
    /// <summary>
    /// Classify <paramref name="rule"/> into one of the
    /// <see cref="RuleKind"/> shapes. The check order matches the
    /// historical executor dispatch:
    /// <see cref="RuleKind.Reference"/> →
    /// <see cref="RuleKind.WrapperFollowedBy"/> →
    /// <see cref="RuleKind.Link"/> →
    /// <see cref="RuleKind.Manual"/> →
    /// <see cref="RuleKind.SlotArchetypeMarker"/> →
    /// <see cref="RuleKind.DirectCopy"/> →
    /// <see cref="RuleKind.Unknown"/>.
    /// </summary>
    public static RuleKind Classify(MappingRule rule)
    {
        if (rule.Reference is not null)
        {
            return RuleKind.Reference;
        }
        if (rule.FollowedBy is { Mappings.Count: > 0 } && rule.With.Type == WithType.None)
        {
            return RuleKind.WrapperFollowedBy;
        }
        if (rule.Link is not null)
        {
            return RuleKind.Link;
        }
        if (rule.Manual is { Count: > 0 })
        {
            return RuleKind.Manual;
        }
        if (rule.SlotArchetype is not null &&
            rule.With.Type == WithType.None &&
            rule.FollowedBy is null &&
            rule.Manual is null &&
            rule.Link is null)
        {
            return RuleKind.SlotArchetypeMarker;
        }
        if (rule.With.OpenEhr is not null &&
            rule.With.Fhir is not null &&
            rule.With.Type != WithType.None)
        {
            return RuleKind.DirectCopy;
        }
        return RuleKind.Unknown;
    }

    /// <summary>
    /// The set of populated <em>primary slots</em> on
    /// <paramref name="rule"/>. A primary slot is one that selects
    /// the rule's executable shape — <see cref="MappingRule.Reference"/>,
    /// <see cref="MappingRule.Link"/>, <see cref="MappingRule.Manual"/>,
    /// a populated <see cref="MappingRule.FollowedBy"/> with
    /// <c>with.type == NONE</c> (wrapper), and a
    /// <see cref="MappingRule.SlotArchetype"/> with no other primary
    /// slot populated.
    /// <para/>
    /// <c>With.OpenEhr</c> and <c>With.Fhir</c> are <em>not</em>
    /// primary slots — they co-exist with every kind. The
    /// canonical KDS bundle's five <c>partOfReference</c> rules
    /// pair <c>link</c> with both <c>with.openehr</c> and
    /// <c>with.fhir</c>; the bundle must continue to load.
    /// <para/>
    /// The loader treats a result with more than one entry as a
    /// load-time error.
    /// </summary>
    public static IReadOnlyList<string> AmbiguousPrimarySlots(MappingRule rule)
    {
        List<string> slots = [];
        if (rule.Reference is not null)
        {
            slots.Add("reference");
        }
        if (rule.Link is not null)
        {
            slots.Add("link");
        }
        if (rule.Manual is { Count: > 0 })
        {
            slots.Add("manual");
        }
        if (rule.FollowedBy is { Mappings.Count: > 0 } && rule.With.Type == WithType.None)
        {
            slots.Add("followedBy");
        }
        return slots;
    }
}
