using System;
using System.CommandLine;
using System.IO;

namespace DotnetFhirConnect.Cli.Tests;

/// <summary>
/// Drives <see cref="Program.BuildRootCommand"/> in-process and
/// captures stdout/stderr through the System.CommandLine v3
/// <c>InvocationConfiguration</c> hooks. Returns the exit code
/// from <c>ParseResult.Invoke</c> — do **not** consult
/// <c>Environment.ExitCode</c> (which isn't set on in-process
/// invocations).
/// </summary>
internal sealed class CliHarness
{
    public string StdOut { get; private set; } = string.Empty;
    public string StdErr { get; private set; } = string.Empty;
    public int ExitCode { get; private set; }

    public CliHarness Run(params string[] args)
    {
        RootCommand root = Program.BuildRootCommand();
        ParseResult parse = root.Parse(args);

        StringWriter outBuf = new StringWriter();
        StringWriter errBuf = new StringWriter();
        parse.InvocationConfiguration.Output = outBuf;
        parse.InvocationConfiguration.Error = errBuf;

        ExitCode = parse.Invoke();
        StdOut = outBuf.ToString();
        StdErr = errBuf.ToString();
        return this;
    }
}
