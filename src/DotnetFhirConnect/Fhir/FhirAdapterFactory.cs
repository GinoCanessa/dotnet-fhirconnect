using System;

namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Picks an <see cref="IFhirAdapter"/> implementation for a given
/// FHIR release. The mapping bundle's <c>spec.version</c> drives
/// selection at engine construction time.
/// </summary>
public static class FhirAdapterFactory
{
    /// <summary>
    /// Return a new adapter for <paramref name="release"/>. R4B and
    /// R5 adapters ship but are stubs in v0.x — calling any of their
    /// methods throws <see cref="NotImplementedException"/> with a
    /// release-tagged message so the engine fails fast and loudly
    /// rather than silently mis-mapping.
    /// </summary>
    public static IFhirAdapter Create(FhirRelease release) => release switch
    {
        FhirRelease.R4 => new R4.R4Adapter(),
        FhirRelease.R4B => new R4B.R4BAdapter(),
        FhirRelease.R5 => new R5.R5Adapter(),
        _ => throw new ArgumentOutOfRangeException(nameof(release), release, "Unknown FHIR release."),
    };
}
