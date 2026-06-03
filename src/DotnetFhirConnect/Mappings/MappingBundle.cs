using System;
using System.Collections.Generic;
using System.IO;
using DotnetFhirConnect.Internal;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// All the FHIRconnect mapping files in one project directory,
/// indexed by their <c>metadata.name</c>.
/// </summary>
public sealed record MappingBundle(
    ContextMapping? Context,
    IReadOnlyDictionary<string, ModelMapping> Models,
    IReadOnlyDictionary<string, ExtensionMapping> Extensions)
{
    /// <summary>
    /// Scan <paramref name="directory"/> for <c>*.yml</c> /
    /// <c>*.yaml</c> files (recursively) and parse each one. The
    /// result has at most one <see cref="Context"/> entry, plus
    /// dictionaries of model and extension mappings keyed by name.
    /// </summary>
    /// <exception cref="FhirConnectFormatException">
    /// Any parse error, or two context files in the same directory
    /// (which would make <see cref="Context"/> ambiguous).
    /// </exception>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
        "Routes through YamlDotNet's representation model (via MappingYamlReader). "
        + "Library is not AOT-publishable in v0.x.")]
    public static MappingBundle Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"FHIRconnect mapping directory not found: '{directory}'.");
        }

        ContextMapping? context = null;
        Dictionary<string, ModelMapping> models = new Dictionary<string, ModelMapping>(StringComparer.Ordinal);
        Dictionary<string, ExtensionMapping> extensions = new Dictionary<string, ExtensionMapping>(StringComparer.Ordinal);

        string[] files = Directory.GetFiles(
            directory, "*.y*ml", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            string ext = Path.GetExtension(file);
            if (!string.Equals(ext, ".yml", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".yaml", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FhirConnectMapping parsed = MappingYamlReader.ReadFile(file);
            switch (parsed)
            {
                case ContextMapping cm when context is not null:
                    throw new FhirConnectFormatException(
                        $"Multiple context files found in bundle: existing '{context.Metadata.Name}' vs. new '{cm.Metadata.Name}' from '{file}'. A bundle directory may contain at most one type:context file.",
                        file);
                case ContextMapping cm:
                    context = cm;
                    break;
                case ModelMapping mm:
                    if (models.ContainsKey(mm.Metadata.Name))
                    {
                        throw new FhirConnectFormatException(
                            $"Duplicate model mapping '{mm.Metadata.Name}' (second copy at '{file}').",
                            file);
                    }
                    models[mm.Metadata.Name] = mm;
                    break;
                case ExtensionMapping em:
                    if (extensions.ContainsKey(em.Metadata.Name))
                    {
                        throw new FhirConnectFormatException(
                            $"Duplicate extension mapping '{em.Metadata.Name}' (second copy at '{file}').",
                            file);
                    }
                    extensions[em.Metadata.Name] = em;
                    break;
                default:
                    throw new FhirConnectFormatException(
                        $"Unexpected mapping type '{parsed.Type}' in '{file}'.",
                        file);
            }
        }

        return new MappingBundle(context, models, extensions);
    }
}
