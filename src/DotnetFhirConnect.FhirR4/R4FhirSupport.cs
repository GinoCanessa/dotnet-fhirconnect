using System.Runtime.CompilerServices;
using DotnetFhirConnect.Fhir;

namespace DotnetFhirConnect.Fhir.R4;

/// <summary>
/// Registers the R4 adapter with <see cref="FhirAdapterFactory"/>.
/// Fires automatically via a module initializer when the
/// <c>DotnetFhirConnect.FhirR4</c> assembly is loaded; call
/// <see cref="Register"/> explicitly from an agnostic host that only
/// touches <see cref="FhirConnectEngine"/> and never a per-release type.
/// </summary>
public static class R4FhirSupport
{
    /// <summary>
    /// Register the R4 adapter factory. Idempotent.
    /// </summary>
    public static void Register() =>
        FhirAdapterFactory.Register(FhirRelease.R4, static () => new R4Adapter());

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage", "CA2255",
        Justification = "Intentional adapter self-registration: loading the " +
            "FhirR4 assembly must populate FhirAdapterFactory so spec.version " +
            "selection resolves without an explicit Register() call.")]
    internal static void AutoRegister() => Register();
}
