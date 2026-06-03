using System.Collections.Generic;
using DotnetFhirConnect.Fhir;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// <c>metadata:</c> block — every mapping file has a name + a
/// free-form version string.
/// </summary>
public sealed record MappingMetadata(string Name, string Version);

/// <summary>
/// <c>spec.openEhrConfig:</c> sub-block on a model mapping —
/// declares the archetype id and an optional ADL revision.
/// </summary>
public sealed record OpenEhrConfig(string Archetype, string? Revision);

/// <summary>
/// <c>spec.fhirConfig:</c> sub-block on a model mapping — declares
/// the FHIR StructureDefinition URL the rules target.
/// </summary>
public sealed record FhirConfig(string StructureDefinition);

/// <summary>
/// <c>spec:</c> block — common to all three file kinds. Only
/// <see cref="System"/> + <see cref="Version"/> are guaranteed
/// present; the remaining fields are file-kind-dependent.
/// </summary>
/// <param name="System">Always <c>"FHIR"</c> in v1.0.0.</param>
/// <param name="Version">The HL7 FHIR release the mapping targets.</param>
/// <param name="Extends">For extension files, the model mapping's
/// <c>metadata.name</c> the extension overlays.</param>
/// <param name="OpenEhrConfig">For model files (and optionally for
/// extension files that re-declare the archetype).</param>
/// <param name="FhirConfig">For model files only.</param>
public sealed record MappingSpec(
    string System,
    FhirRelease Version,
    string? Extends,
    OpenEhrConfig? OpenEhrConfig,
    FhirConfig? FhirConfig);

/// <summary>
/// One entry inside a <c>manual:</c> block — assigns a literal
/// value to a path under the rule's openEHR or FHIR sub-root.
/// </summary>
public sealed record ManualField(string Path, string Value);

/// <summary>
/// A named bag of <c>manual:</c> assignments on one rule. The same
/// rule may carry both <see cref="Fhir"/> and <see cref="OpenEhr"/>
/// lists (constants for both sides).
/// </summary>
public sealed record ManualEntry(
    string Name,
    IReadOnlyList<ManualField>? Fhir,
    IReadOnlyList<ManualField>? OpenEhr);

/// <summary>
/// The <c>with:</c> block. Both path slots are nullable: real
/// mappings contain <c>with: { fhir: "coding" }</c> with no openehr
/// path inside nested <c>followedBy</c> blocks.
/// </summary>
public sealed record WithBlock(string? Fhir, string? OpenEhr, WithType Type);

/// <summary>
/// Wrapper around the nested <c>followedBy.mappings:</c> rule list,
/// kept as a distinct type so call sites cannot pass it where a bare
/// list would do.
/// </summary>
public sealed record FollowedBy(IReadOnlyList<MappingRule> Mappings);

/// <summary>
/// The <c>link:</c> block — describes the openEHR
/// <c>Composition.Links</c> entry the rule reads / writes alongside
/// the mirroring FHIR field.
/// </summary>
public sealed record LinkSpec(string Meaning, string Type);

/// <summary>
/// The <c>reference:</c> block — used on extension rules that wire
/// up a FHIR reference target (e.g. encounter) with nested
/// mappings against the referenced resource's root.
/// </summary>
public sealed record ReferenceSpec(
    string? ResourceType,
    IReadOnlyList<MappingRule> Mappings);

/// <summary>
/// One <c>mappings:</c> entry. Carries every optional rule-kind
/// slot — only a subset are populated on any given rule. The rule
/// kind is determined by which slots are set; the engine dispatcher
/// (Phase 6a) pattern-matches on populated combinations.
/// </summary>
public sealed record MappingRule(
    string Name,
    WithBlock With,
    string? Unidirectional,
    IReadOnlyList<ManualEntry>? Manual,
    FollowedBy? FollowedBy,
    LinkSpec? Link,
    ReferenceSpec? Reference,
    string? SlotArchetype,
    ExtensionAction? Extension,
    string? FhirCondition);

/// <summary>
/// <c>context:</c> block on a context file.
/// </summary>
public sealed record ContextSpec(
    ProfileRef Profile,
    TemplateRef Template,
    IReadOnlyList<string> Archetypes,
    IReadOnlyList<string> Extensions,
    string Start);

/// <summary>
/// The FHIR profile a context file points its mappings at.
/// </summary>
public sealed record ProfileRef(string Url, string? Version);

/// <summary>
/// The openEHR Operational Template a context file points its
/// mappings at.
/// </summary>
public sealed record TemplateRef(string Id, string? SemVer);
