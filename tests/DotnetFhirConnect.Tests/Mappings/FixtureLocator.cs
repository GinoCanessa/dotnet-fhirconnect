using System.IO;
using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Helpers shared by the Phase 3 loader tests for resolving the
/// vendored fixture paths under the test run's bin directory.
/// </summary>
internal static class FixtureLocator
{
    public static string VitalStatusBundleRoot => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "vital-status");

    public static string ModelFile => Path.Combine(
        VitalStatusBundleRoot, "model", "vital_status.v1.yml");

    public static string ProjectDir => Path.Combine(
        VitalStatusBundleRoot, "project");

    public static string ContextFile => Path.Combine(
        ProjectDir, "KDS_Vitalstatus.context.yaml");

    public static string KdsCompositionFile => Path.Combine(
        ProjectDir, "KDS_composition.yml");

    public static string KdsVitalsignsFile => Path.Combine(
        ProjectDir, "KDS_vitalsigns.yml");
}
