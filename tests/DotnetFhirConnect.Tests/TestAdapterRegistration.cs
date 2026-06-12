using System.Runtime.CompilerServices;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Fhir.R4B;
using DotnetFhirConnect.Fhir.R5;

namespace DotnetFhirConnect.Tests;

/// <summary>
/// Test-assembly module initializer that registers all three FHIR
/// adapters before any test runs. Guarantees
/// <see cref="DotnetFhirConnect.Fhir.FhirAdapterFactory.Create"/> and
/// the engine facades resolve deterministically regardless of which
/// test first touches a per-release type.
/// </summary>
internal static class TestAdapterRegistration
{
    [ModuleInitializer]
    internal static void RegisterAll()
    {
        R4FhirSupport.Register();
        R4BFhirSupport.Register();
        R5FhirSupport.Register();
    }
}
