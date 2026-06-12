using System.Runtime.CompilerServices;
using DotnetFhirConnect.Fhir;

namespace DotnetFhirConnect.Fhir.R5;

/// <summary>
/// Registers the R5 adapter with <see cref="FhirAdapterFactory"/>.
/// See <c>R4FhirSupport</c> for the contract.
/// </summary>
public static class R5FhirSupport
{
    /// <summary>
    /// Register the R5 adapter factory. Idempotent.
    /// </summary>
    public static void Register() =>
        FhirAdapterFactory.Register(FhirRelease.R5, static () => new R5Adapter());

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2255",
        Justification = "Intentional adapter self-registration: loading the " +
            "FhirR5 assembly must populate FhirAdapterFactory so spec.version " +
            "selection resolves without an explicit Register() call.")]
    internal static void AutoRegister() => Register();
}
