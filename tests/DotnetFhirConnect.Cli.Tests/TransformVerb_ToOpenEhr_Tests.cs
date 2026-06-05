using System;
using System.IO;
using Xunit;

namespace DotnetFhirConnect.Cli.Tests;

/// <summary>
/// Phase 4 — CLI happy-path + sad-path coverage for
/// <c>transform --direction to-openehr</c>.
/// </summary>
public sealed class TransformVerb_ToOpenEhr_Tests
{
    [Fact]
    public void ToOpenEhr_FromObservation_ToStdout_ProducesComposition()
    {
        CliHarness h = new CliHarness().Run(
            "transform",
            "--direction", "to-openehr",
            "--mapping", CliFixtures.FixtureRoot,
            "--input", CliFixtures.ObservationFile,
            "--output", "-");

        Assert.Equal(0, h.ExitCode);
        Assert.Contains("\"_type\":\"COMPOSITION\"", h.StdOut);
        // Confirm the vital_status evaluation archetype was wrapped.
        Assert.Contains("EVALUATION.vital_status.v1", h.StdOut);
    }

    [Fact]
    public void ToOpenEhr_ToTempFile_WritesValidComposition()
    {
        string outFile = Path.Combine(Path.GetTempPath(), $"to-openehr-{Guid.NewGuid():N}.json");
        try
        {
            CliHarness h = new CliHarness().Run(
                "transform",
                "--direction", "to-openehr",
                "--mapping", CliFixtures.FixtureRoot,
                "--input", CliFixtures.ObservationFile,
                "--output", outFile);

            Assert.Equal(0, h.ExitCode);
            Assert.True(File.Exists(outFile));
            string content = File.ReadAllText(outFile);
            Assert.Contains("\"_type\":\"COMPOSITION\"", content);
        }
        finally
        {
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }
        }
    }
}
