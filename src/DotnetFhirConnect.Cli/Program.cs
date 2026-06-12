using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using System.Runtime.CompilerServices;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Fhir.R4B;
using DotnetFhirConnect.Fhir.R5;

[assembly: InternalsVisibleTo("DotnetFhirConnect.Cli.Tests")]

namespace DotnetFhirConnect.Cli;

internal static class Program
{
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "v0.x library is not AOT-publishable; CLI inherits the same posture.")]
    public static int Main(string[] args)
    {
        RootCommand root = BuildRootCommand();
        ParseResult parse = root.Parse(args);
        return parse.Invoke();
    }

    /// <summary>
    /// Build the <c>fhirconnect</c> root command with the two
    /// concrete verbs (<c>transform</c>, <c>validate</c>) and the
    /// auto-generated <c>--help</c>. Public so the in-process test
    /// harness can invoke it without spawning <c>dotnet run</c>.
    /// </summary>
    [RequiresUnreferencedCode("Verbs route through the non-AOT library surface.")]
    public static RootCommand BuildRootCommand()
    {
        // Register every FHIR release adapter up front. The agnostic
        // to-openehr path never touches a per-release type, so the
        // per-package module initializers may not have fired; register
        // here (idempotent) so spec.version selection is deterministic.
        R4FhirSupport.Register();
        R4BFhirSupport.Register();
        R5FhirSupport.Register();

        RootCommand root = new RootCommand("Bidirectional openEHR ↔ FHIR transformation via FHIRconnect.");
        root.Add(BuildValidateCommand());
        root.Add(BuildTransformCommand());
        return root;
    }

    [RequiresUnreferencedCode("Verbs route through the non-AOT library surface.")]
    private static Command BuildValidateCommand()
    {
        Option<string> mapping = new Option<string>("--mapping", "-m")
        {
            Description = "Path to a FHIRconnect mapping file or bundle directory.",
            Required = true,
        };
        Option<string> format = new Option<string>("--format")
        {
            Description = "Output format: text (default) or json.",
            DefaultValueFactory = _ => "text",
        };

        Command cmd = new Command("validate", "Validate a FHIRconnect mapping file or bundle.");
        cmd.Add(mapping);
        cmd.Add(format);
        cmd.SetAction((ParseResult result) =>
        {
            string mappingPath = result.GetValue(mapping) ?? string.Empty;
            string fmt = result.GetValue(format) ?? "text";
            TextWriter output = result.InvocationConfiguration.Output;
            TextWriter error = result.InvocationConfiguration.Error;
            return Verbs.ValidateVerb.Run(mappingPath, fmt, output, error);
        });
        return cmd;
    }

    [RequiresUnreferencedCode("Verbs route through the non-AOT library surface.")]
    private static Command BuildTransformCommand()
    {
        Option<string> direction = new Option<string>("--direction", "-d")
        {
            Description = "Transform direction: to-fhir or to-openehr.",
            Required = true,
        };
        Option<string> mapping = new Option<string>("--mapping", "-m")
        {
            Description = "Path to the FHIRconnect mapping bundle root.",
            Required = true,
        };
        Option<string> input = new Option<string>("--input", "-i")
        {
            Description = "Path to the input file (openEHR canonical JSON for to-fhir, FHIR JSON for to-openehr).",
            Required = true,
        };
        Option<string> output = new Option<string>("--output", "-o")
        {
            Description = "Output file path. Use '-' to write to stdout.",
            Required = true,
        };

        Command cmd = new Command("transform", "Transform openEHR ↔ FHIR via a FHIRconnect mapping bundle.");
        cmd.Add(direction);
        cmd.Add(mapping);
        cmd.Add(input);
        cmd.Add(output);
        cmd.SetAction((ParseResult result) =>
        {
            string dir = result.GetValue(direction) ?? string.Empty;
            string mappingPath = result.GetValue(mapping) ?? string.Empty;
            string inputPath = result.GetValue(input) ?? string.Empty;
            string outputPath = result.GetValue(output) ?? string.Empty;
            TextWriter outW = result.InvocationConfiguration.Output;
            TextWriter errW = result.InvocationConfiguration.Error;
            return Verbs.TransformVerb.Run(dir, mappingPath, inputPath, outputPath, outW, errW);
        });
        return cmd;
    }
}
