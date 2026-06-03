using System;
using System.IO;
using Xunit;

namespace DotnetFhirConnect.Cli.Tests;

public sealed class CliValidateTests
{
    [Fact]
    public void Validate_VitalStatusModel_ReturnsZeroWithValidMessage()
    {
        CliHarness h = new CliHarness().Run("validate", "--mapping", CliFixtures.ModelFile);
        Assert.Equal(0, h.ExitCode);
        Assert.Contains("Valid", h.StdOut);
    }

    [Fact]
    public void Validate_MutatedFile_ReturnsOneAndIncludesPointer()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"cli-validate-bad-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, """
            grammar: FHIRConnect/v0.9.0
            type: model
            metadata:
              name: dummy
              version: "0"
            spec:
              system: FHIR
              version: R4
            mappings: []
            """);
        try
        {
            CliHarness h = new CliHarness().Run("validate", "--mapping", tempFile);
            Assert.Equal(1, h.ExitCode);
            Assert.Contains("/grammar", h.StdOut);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_JsonFormat_EmitsJsonPayload()
    {
        CliHarness h = new CliHarness().Run("validate", "--mapping", CliFixtures.ModelFile, "--format", "json");
        Assert.Equal(0, h.ExitCode);
        Assert.Contains("\"isValid\": true", h.StdOut);
    }
}
