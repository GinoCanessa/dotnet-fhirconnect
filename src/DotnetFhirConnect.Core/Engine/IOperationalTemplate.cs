namespace DotnetFhirConnect.Engine;

/// <summary>
/// Internal seam over a loaded openEHR operational template (OPT). The
/// engine uses this only to resolve human-readable element names for the
/// openEHR side of a transform; it is deliberately <c>internal</c> (the
/// library ships no public OPT-loading API in v0.x) and optional at every
/// call site (when absent the writer falls back to the bare at-code).
/// </summary>
internal interface IOperationalTemplate
{
    /// <summary>
    /// The friendly template id (e.g. <c>KDS_Vitalstatus</c>) when the OPT
    /// records one, otherwise the root archetype concept id.
    /// </summary>
    string TemplateId { get; }

    /// <summary>
    /// Resolve the display name for <paramref name="atCode"/> within the
    /// component archetype identified by <paramref name="archetypeId"/>
    /// (e.g. <c>openEHR-EHR-EVALUATION.vital_status.v1</c>). Returns
    /// <c>null</c> when the term cannot be resolved, in which case the
    /// caller uses the bare at-code.
    /// </summary>
    /// <param name="archetypeId">The active component archetype id, or
    /// <c>null</c>/empty to search only the root terminology.</param>
    /// <param name="atCode">The at-code whose display name is wanted.</param>
    string? ResolveElementName(string? archetypeId, string atCode);
}
