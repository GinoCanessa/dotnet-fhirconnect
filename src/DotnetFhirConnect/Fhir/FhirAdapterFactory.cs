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
    /// Return a new adapter for <paramref name="release"/>. All three
    /// releases ship working adapters: R4 via <see cref="R4.R4Adapter"/>,
    /// and R4B / R5 via implementations sharing a common adapter core
    /// plus per-release Observation shims.
    /// </summary>
    public static IFhirAdapter Create(FhirRelease release) => release switch
    {
        FhirRelease.R4 => new R4.R4Adapter(),
        FhirRelease.R4B => new R4B.R4BAdapter(),
        FhirRelease.R5 => new R5.R5Adapter(),
        _ => throw new ArgumentOutOfRangeException(nameof(release), release, "Unknown FHIR release."),
    };
}
