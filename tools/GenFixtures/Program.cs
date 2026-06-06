extern alias coreR4;
using System;
using System.IO;
using System.Text;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Mappings;
using DotnetOpenEhr.Serialization.Json;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

namespace DotnetFhirConnect.Tools.GenFixtures;

/// <summary>
/// Regenerates the canonical vital-status R4 Observation fixture from
/// the canonical Composition + project bundle. Run after any mapping
/// change that affects the ToFhir output. CI does not run this.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        string repoRoot = ResolveRepoRoot();
        string fixturesDir = Path.Combine(repoRoot, "tests", "fixtures", "vital-status");
        string compositionPath = Path.Combine(fixturesDir, "samples", "vital-status.composition.canonical.json");
        string outputPath = Path.Combine(fixturesDir, "samples", "vital-status.observation.r4.json");

        if (!File.Exists(compositionPath))
        {
            Console.Error.WriteLine($"GenFixtures: composition fixture not found at '{compositionPath}'.");
            return 1;
        }

        string compositionJson = File.ReadAllText(compositionPath);
        OpenEhrComposition composition = OpenEhrJson.ParseComposition(compositionJson)
            ?? throw new InvalidOperationException("Composition fixture did not parse.");

        string modelDir = Path.Combine(fixturesDir, "model");
        string projectDir = Path.Combine(fixturesDir, "project");
        MappingBundle project = (MappingBundle)FhirConnectMapping.Load(projectDir);
        Dictionary<string, ModelMapping> models = new Dictionary<string, ModelMapping>(project.Models, StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(modelDir, "*.y*ml", SearchOption.AllDirectories))
        {
            object loaded = FhirConnectMapping.Load(file);
            if (loaded is ModelMapping m)
            {
                models[m.Metadata.Name] = m;
            }
        }
        MappingBundle bundle = new MappingBundle(project.Context, models, project.Extensions);

        R4Engine engine = new R4Engine(bundle);
        Hl7.Fhir.Model.Resource resource = engine.ToFhir(composition);
        // No-fake-data policy: the produced fixture reflects exactly
        // what the engine emits. Adapter-side tests that need a
        // parser-complete Observation use a sibling
        // `*.parseable.json` sample (added on demand if Firely's
        // parser ever rejects the engine output).
        string observationJson = engine.Core.Adapter.SerializeResource(resource);

        File.WriteAllText(outputPath, observationJson, new UTF8Encoding(false));
        Console.WriteLine($"GenFixtures: wrote {outputPath} ({observationJson.Length} chars).");
        return 0;
    }

    private static string ResolveRepoRoot()
    {
        // Walk up from the executable until we find a marker. The
        // tool always runs from bin/<conf>/<tfm>/, so the marker is
        // a few levels up.
        DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DotnetFhirConnect.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "GenFixtures: could not locate repository root (no DotnetFhirConnect.slnx upstream).");
    }
}
