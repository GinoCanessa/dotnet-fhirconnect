using System;
using System.IO;
using Xunit;

namespace DotnetFhirConnect.Cli.Tests;

public sealed class CliTransformTests
{
    [Fact]
    public void Transform_ToFhirFromCanonicalJson_ToStdout_ProducesObservation()
    {
        CliHarness h = new CliHarness().Run(
            "transform",
            "--direction", "to-fhir",
            "--mapping", CliFixtures.FixtureRoot,
            "--input", CliFixtures.CompositionFile,
            "--output", "-");

        Assert.Equal(0, h.ExitCode);
        Assert.Contains("\"resourceType\":\"Observation\"", h.StdOut);
        Assert.Contains("\"effectiveDateTime\"", h.StdOut);
    }

    [Fact]
    public void Transform_MissingInputFile_ReturnsIoErrorWithDiagnostic()
    {
        string bogus = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.json");
        CliHarness h = new CliHarness().Run(
            "transform",
            "--direction", "to-fhir",
            "--mapping", CliFixtures.FixtureRoot,
            "--input", bogus,
            "--output", "-");

        Assert.Equal(2, h.ExitCode); // IoOrParseError
        Assert.Contains("input file not found", h.StdErr);
    }
}
