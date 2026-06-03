using System.Collections.Generic;
using System.IO;
using DotnetFhirConnect.Internal;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// Common abstract base for the three FHIRconnect mapping file kinds
/// (<see cref="ModelMapping"/>, <see cref="ContextMapping"/>,
/// <see cref="ExtensionMapping"/>). Carries the four blocks shared
/// across all three kinds; the kind-specific blocks live on the
/// concrete subclasses.
/// </summary>
public abstract record FhirConnectMapping
{
    private protected FhirConnectMapping(
        FhirConnectGrammar grammar,
        MappingType type,
        MappingMetadata metadata,
        MappingSpec spec)
    {
        Grammar = grammar;
        Type = type;
        Metadata = metadata;
        Spec = spec;
    }

    /// <summary>The <c>grammar:</c> declaration (only <c>FHIRConnect/v1.0.0</c> is supported).</summary>
    public FhirConnectGrammar Grammar { get; }

    /// <summary>The <c>type:</c> declaration — model / context / extension.</summary>
    public MappingType Type { get; }

    /// <summary>The <c>metadata:</c> block.</summary>
    public MappingMetadata Metadata { get; }

    /// <summary>The <c>spec:</c> block.</summary>
    public MappingSpec Spec { get; }

    /// <summary>
    /// Load a single mapping file or a directory of mapping files.
    /// If <paramref name="path"/> is a file, parses one mapping and
    /// returns it. If <paramref name="path"/> is a directory, scans
    /// for <c>*.yml</c> / <c>*.yaml</c> and returns a
    /// <see cref="MappingBundle"/>.
    /// </summary>
    /// <exception cref="FhirConnectFormatException">
    /// Any parse error, unsupported grammar, or unrecognised
    /// <c>type:</c> declaration.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// <paramref name="path"/> does not exist.
    /// </exception>
    public static object Load(string path)
    {
        if (Directory.Exists(path))
        {
            return MappingBundle.Load(path);
        }

        if (File.Exists(path))
        {
            return MappingYamlReader.ReadFile(path);
        }

        throw new FileNotFoundException(
            $"FHIRconnect mapping path not found: '{path}'.", path);
    }
}

/// <summary>
/// A <c>type: model</c> mapping file.
/// </summary>
public sealed record ModelMapping : FhirConnectMapping
{
    /// <summary>The ordered list of model-level mapping rules.</summary>
    public IReadOnlyList<MappingRule> Mappings { get; }

    /// <summary>Initializes a new model mapping record.</summary>
    public ModelMapping(
        FhirConnectGrammar grammar,
        MappingMetadata metadata,
        MappingSpec spec,
        IReadOnlyList<MappingRule> mappings)
        : base(grammar, MappingType.Model, metadata, spec)
    {
        Mappings = mappings;
    }
}

/// <summary>
/// A <c>type: context</c> mapping file.
/// </summary>
public sealed record ContextMapping : FhirConnectMapping
{
    /// <summary>The <c>context:</c> block.</summary>
    public ContextSpec Context { get; }

    /// <summary>Initializes a new context mapping record.</summary>
    public ContextMapping(
        FhirConnectGrammar grammar,
        MappingMetadata metadata,
        MappingSpec spec,
        ContextSpec context)
        : base(grammar, MappingType.Context, metadata, spec)
    {
        Context = context;
    }
}

/// <summary>
/// A <c>type: extension</c> mapping file.
/// </summary>
public sealed record ExtensionMapping : FhirConnectMapping
{
    /// <summary>The ordered list of extension-level mapping rules.</summary>
    public IReadOnlyList<MappingRule> Mappings { get; }

    /// <summary>Initializes a new extension mapping record.</summary>
    public ExtensionMapping(
        FhirConnectGrammar grammar,
        MappingMetadata metadata,
        MappingSpec spec,
        IReadOnlyList<MappingRule> mappings)
        : base(grammar, MappingType.Extension, metadata, spec)
    {
        Mappings = mappings;
    }
}
