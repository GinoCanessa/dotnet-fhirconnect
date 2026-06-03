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
internal sealed record BindingContext(
    Composition Composition,
    Pathable Archetype,
    object OpenEhrRoot,
    object Resource,
    string FhirRoot)
{
    /// <summary>
    /// Rebind <see cref="OpenEhrRoot"/> and <see cref="FhirRoot"/>
    /// to the values inside a <c>followedBy.with</c> block. Both
    /// arguments are optional — keep the current binding when null.
    /// </summary>
    public BindingContext PushFollowedBy(object? openEhrRoot, string? fhirRoot)
    {
        return this with
        {
            OpenEhrRoot = openEhrRoot ?? OpenEhrRoot,
            FhirRoot = fhirRoot ?? FhirRoot,
        };
    }
}
