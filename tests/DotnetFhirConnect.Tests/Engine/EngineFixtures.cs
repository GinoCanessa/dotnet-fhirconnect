using System.IO;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;
using DotnetOpenEhr.Serialization.Json;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Helpers for the engine tests: locate the vital_status fixtures
/// in the test bin dir and load both halves of the bundle.
/// </summary>
internal static class EngineFixtures
{
    public static string FixtureRoot => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "vital-status");

    public static string CompositionFile => Path.Combine(
        FixtureRoot, "samples", "vital-status.composition.canonical.json");

    public static string ObservationFile => Path.Combine(
        FixtureRoot, "samples", "vital-status.observation.r4.json");

    /// <summary>
    /// The vendored vital_status v1 mapping bundle plus the KDS
    /// project context + extensions. Phase 6a only applies model
    /// rules but still loads everything so the engine can see the
    /// bundle as a whole.
    /// </summary>
    public static MappingBundle LoadBundle()
    {
        // Load the model file and project directory and merge.
        ModelMapping model = (ModelMapping)FhirConnectMapping.Load(
            Path.Combine(FixtureRoot, "model", "vital_status.v1.yml"));
        MappingBundle project = (MappingBundle)FhirConnectMapping.Load(
            Path.Combine(FixtureRoot, "project"));
        Dictionary<string, ModelMapping> models = new Dictionary<string, ModelMapping>(project.Models)
        {
            [model.Metadata.Name] = model,
        };
        return new MappingBundle(project.Context, models, project.Extensions);
    }

    public static OpenEhrComposition LoadComposition()
    {
        string json = File.ReadAllText(CompositionFile);
        return OpenEhrJson.ParseComposition(json)
            ?? throw new System.InvalidOperationException("Composition fixture parse returned null.");
    }
}
