using System.IO;
using Xunit;

namespace DotnetFhirConnect.Cli.Tests;

/// <summary>
/// CLI fixture locator — relies on the same MSBuild fixture-copy
/// wiring as the library tests.
/// </summary>
internal static class CliFixtures
{
    public static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "vital-status");

    public static string ModelFile => Path.Combine(
        FixtureRoot, "model", "vital_status.v1.yml");

    public static string CompositionFile => Path.Combine(
        FixtureRoot, "samples", "vital-status.composition.canonical.json");

    public static string ObservationFile => Path.Combine(
        FixtureRoot, "samples", "vital-status.observation.r4.json");

    public static string ProjectDir => Path.Combine(FixtureRoot, "project");
}
