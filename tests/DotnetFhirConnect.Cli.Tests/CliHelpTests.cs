using Xunit;

namespace DotnetFhirConnect.Cli.Tests;

public sealed class CliHelpTests
{
    [Fact]
    public void Help_ListsTransformAndValidate()
    {
        CliHarness h = new CliHarness().Run("--help");
        // --help is treated as a successful invocation by
        // System.CommandLine. Help output goes to stdout.
        Assert.Equal(0, h.ExitCode);
        Assert.Contains("transform", h.StdOut);
        Assert.Contains("validate", h.StdOut);
    }

    [Fact]
    public void NoArgs_ShowsHelpOrUsage_WithNonZeroExit()
    {
        CliHarness h = new CliHarness().Run(System.Array.Empty<string>());
        // Without arguments, System.CommandLine emits help to stderr
        // and returns a non-zero exit code.
        Assert.NotEqual(0, h.ExitCode);
    }
}
