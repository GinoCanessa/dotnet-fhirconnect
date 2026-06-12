using System.Runtime.CompilerServices;
using DotnetFhirConnect.Fhir;

namespace DotnetFhirConnect.Fhir.R4B;

/// <summary>
/// Registers the R4B adapter with <see cref="FhirAdapterFactory"/>.
/// See <c>R4FhirSupport</c> for the contract.
/// </summary>
public static class R4BFhirSupport
{
    /// <summary>
    /// Register the R4B adapter factory. Idempotent.
    /// </summary>
    public static void Register() =>
        FhirAdapterFactory.Register(FhirRelease.R4B, static () => new R4BAdapter());

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2255",
        Justification = "Intentional adapter self-registration: loading the " +
            "FhirR4B assembly must populate FhirAdapterFactory so spec.version " +
            "selection resolves without an explicit Register() call.")]
    internal static void AutoRegister() => Register();
}
