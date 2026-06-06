using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Threaded through the rule executor as the resolution context for
/// the FHIRconnect path prefixes (<c>$composition</c>,
/// <c>$archetype</c>, <c>$openEHRRoot</c>, <c>$fhirRoot</c>,
/// <c>$resource</c>). Immutable — nested <c>followedBy</c> blocks
/// allocate a new context with the appropriate sub-roots rebound.
/// </summary>
/// <param name="Composition">The Composition root being walked.</param>
/// <param name="Archetype">The model mapping's start archetype
/// entry inside <see cref="Composition"/>.</param>
/// <param name="OpenEhrRoot">The current rule's openEHR sub-root.
/// Defaults to <see cref="Archetype"/>; rebound by enclosing
/// <c>followedBy.with.openehr</c>.</param>
/// <param name="Resource">The FHIR resource being built (or read,
/// in the reverse direction). Boxed because the three Firely
/// packages have no shared base type.</param>
/// <param name="FhirRoot">The current rule's FHIR sub-root path
/// (e.g. <c>"$resource"</c>, <c>"$resource.note"</c>). Used to
/// resolve nested <c>with.fhir</c> values inside <c>followedBy</c>.</param>
/// <param name="ReferenceRoot">The openEHR-side value resolved by
/// the enclosing <c>reference</c> rule, addressable as
/// <c>$reference</c> inside the nested mapping list. Null outside a
/// reference scope.</param>
/// <param name="InReferenceRecursion">True when the current context
/// was reached by descending into a <c>reference</c> rule's nested
/// mappings. Used by <c>MappingRuleExecutor.ExecuteLink</c> to scope
/// its "swallow adapter failure" tolerance — link rules at the top
/// level must surface adapter errors loudly; only nested-in-reference
/// link rules may silently no-op (the adapter does not model every
/// reference-shaped link target on <c>ResourceReference</c>).</param>
internal sealed record BindingContext(
    Composition Composition,
    Pathable Archetype,
    object OpenEhrRoot,
    object Resource,
    string FhirRoot,
    object? ReferenceRoot = null,
    bool InReferenceRecursion = false)
{
    /// <summary>
    /// Rebind <see cref="OpenEhrRoot"/> and <see cref="FhirRoot"/>
    /// to the values inside a <c>followedBy.with</c> block. Both
    /// arguments are optional — keep the current binding when null.
    /// Propagates the current <see cref="InReferenceRecursion"/>
    /// flag — followedBy nesting inside a reference scope stays
    /// inside the reference scope.
    /// </summary>
    public BindingContext PushFollowedBy(object? openEhrRoot, string? fhirRoot)
    {
        return this with
        {
            OpenEhrRoot = openEhrRoot ?? OpenEhrRoot,
            FhirRoot = fhirRoot ?? FhirRoot,
        };
    }

    /// <summary>
    /// Push into a <c>reference</c> rule's nested mapping scope:
    /// rebinds <see cref="Resource"/> to the freshly-built
    /// <c>ResourceReference</c>, <see cref="FhirRoot"/> to its
    /// path, <see cref="ReferenceRoot"/> to the openEHR-side value
    /// the <c>$reference</c> prefix should resolve against, and
    /// sets <see cref="InReferenceRecursion"/> to <c>true</c> so
    /// nested <c>link</c> rules know they may swallow adapter
    /// failures.
    /// </summary>
    public BindingContext PushReference(object resource, string fhirRoot, object? referenceRoot)
    {
        return this with
        {
            Resource = resource,
            FhirRoot = fhirRoot,
            ReferenceRoot = referenceRoot,
            InReferenceRecursion = true,
        };
    }
}
