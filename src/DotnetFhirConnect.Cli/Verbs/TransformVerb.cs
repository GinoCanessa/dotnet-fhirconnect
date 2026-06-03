using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Serialization.Json;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

namespace DotnetFhirConnect.Cli.Verbs;

internal static class TransformVerb
{
    [RequiresUnreferencedCode(
        "Routes through FhirConnectEngine + Firely + DotnetOpenEhr serializers; "
        + "library is not AOT-publishable in v0.x.")]
    public static int Run(
        string direction,
        string mappingPath,
        string inputPath,
        string outputPath,
        TextWriter output,
        TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(direction) ||
            string.IsNullOrWhiteSpace(mappingPath) ||
            string.IsNullOrWhiteSpace(inputPath) ||
            string.IsNullOrWhiteSpace(outputPath))
        {
            error.WriteLine("transform: --direction, --mapping, --input, and --output are all required.");
            return ExitCodes.UsageError;
        }

        if (!Directory.Exists(mappingPath) && !File.Exists(mappingPath))
        {
            error.WriteLine($"transform: mapping path not found: '{mappingPath}'.");
            return ExitCodes.IoOrParseError;
        }
        if (!File.Exists(inputPath))
        {
            error.WriteLine($"transform: input file not found: '{inputPath}'.");
            return ExitCodes.IoOrParseError;
        }

        if (!string.Equals(direction, "to-fhir", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(direction, "to-openehr", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"transform: unknown direction '{direction}'. Expected to-fhir or to-openehr.");
            return ExitCodes.UsageError;
        }

        try
        {
            MappingBundle bundle = LoadBundle(mappingPath);

            if (string.Equals(direction, "to-fhir", StringComparison.OrdinalIgnoreCase))
            {
                return RunToFhir(bundle, inputPath, outputPath, output, error);
            }

            // to-openehr — v0.x: not supported
            error.WriteLine(
                "transform: --direction to-openehr is not implemented in v0.x. " +
                "v0.x ships the to-fhir direction only.");
            return ExitCodes.TransformError;
        }
        catch (FileNotFoundException ex)
        {
            error.WriteLine($"transform: {ex.Message}");
            return ExitCodes.IoOrParseError;
        }
        catch (Exception ex)
        {
            error.WriteLine($"transform: {ex.GetType().Name}: {ex.Message}");
            return ExitCodes.TransformError;
        }
    }

    [RequiresUnreferencedCode("See Run.")]
    private static int RunToFhir(
        MappingBundle bundle,
        string inputPath,
        string outputPath,
        TextWriter output,
        TextWriter error)
    {
        string inputJson = File.ReadAllText(inputPath);
        OpenEhrComposition? composition = OpenEhrJson.ParseComposition(inputJson);
        if (composition is null)
        {
            error.WriteLine($"transform: input '{inputPath}' did not parse as an openEHR canonical Composition.");
            return ExitCodes.IoOrParseError;
        }

        R4Engine engine = new R4Engine(bundle);
        Resource resource = engine.ToFhir(composition);

        string serialized = engine.Core.Adapter.SerializeResource(resource);
        WriteOutput(serialized, outputPath, output);
        return ExitCodes.Success;
    }

    [RequiresUnreferencedCode(
        "Loads the bundle via FhirConnectMapping.Load — non-AOT-clean per v0.x posture.")]
    private static MappingBundle LoadBundle(string path)
    {
        // If the user pointed us at a directory, do the same merge
        // EngineFixtures uses in tests: pull in any sibling model
        // file from <root>/model alongside the project directory.
        if (Directory.Exists(path))
        {
            string projectDir = Path.Combine(path, "project");
            string modelDir = Path.Combine(path, "model");
            if (Directory.Exists(projectDir) && Directory.Exists(modelDir))
            {
                MappingBundle project = (MappingBundle)FhirConnectMapping.Load(projectDir);
                Dictionary<string, ModelMapping> models = new Dictionary<string, ModelMapping>(project.Models);
                foreach (string file in Directory.EnumerateFiles(modelDir, "*.y*ml", SearchOption.AllDirectories))
                {
                    object loaded = FhirConnectMapping.Load(file);
                    if (loaded is ModelMapping m)
                    {
                        models[m.Metadata.Name] = m;
                    }
                }
                return new MappingBundle(project.Context, models, project.Extensions);
            }
            object justBundle = FhirConnectMapping.Load(path);
            return (MappingBundle)justBundle;
        }

        // Single file: must be a model mapping or a context/extension wrapped in
        // a minimal bundle.
        object loadedFile = FhirConnectMapping.Load(path);
        if (loadedFile is ModelMapping single)
        {
            Dictionary<string, ModelMapping> models = new Dictionary<string, ModelMapping>(StringComparer.Ordinal)
            {
                [single.Metadata.Name] = single,
            };
            return new MappingBundle(null, models, new Dictionary<string, ExtensionMapping>(StringComparer.Ordinal));
        }
        throw new InvalidOperationException(
            $"transform: --mapping must point at a model file or a bundle directory (got {loadedFile?.GetType().Name}).");
    }

    private static void WriteOutput(string content, string outputPath, TextWriter stdout)
    {
        if (string.Equals(outputPath, "-", StringComparison.Ordinal))
        {
            stdout.WriteLine(content);
            return;
        }
        File.WriteAllText(outputPath, content, new UTF8Encoding(false));
    }
}
