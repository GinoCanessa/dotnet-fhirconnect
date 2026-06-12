using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Mappings;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DotnetFhirConnect.Internal;

/// <summary>
/// Streaming YAML→typed-record reader for FHIRconnect v1.0.0
/// mapping files. Uses only <see cref="YamlStream"/>/<see cref="YamlNode"/>
/// (no reflection-driven deserializer) so the loader stays trim- and
/// AOT-safe. Internal: consumed by <see cref="FhirConnectMapping.Load(string)"/>
/// and (in Phase 4) by the validator.
/// </summary>
internal static class MappingYamlReader
{
    private const string SupportedGrammar = "FHIRConnect/v1.0.0";

    /// <summary>
    /// Parses one mapping file from disk and returns the appropriate
    /// <see cref="FhirConnectMapping"/> subclass based on the
    /// <c>type:</c> declaration.
    /// </summary>
    [RequiresUnreferencedCode(
        "Calls YamlDotNet's streaming RepresentationModel. The reader itself avoids "
        + "reflection-driven deserialization, but YamlDotNet still pulls in TypeConverter / "
        + "Reflection at the assembly level so trim warnings are propagated through this entry "
        + "point. Mark consumers that go through Load with the same attribute or stay on "
        + "non-AOT builds.")]
    public static FhirConnectMapping ReadFile(string filePath)
    {
        using StreamReader reader = new StreamReader(filePath);
        return ReadFromReader(reader, filePath);
    }

    [RequiresUnreferencedCode("See ReadFile.")]
    internal static FhirConnectMapping ReadFromReader(TextReader reader, string filePath)
    {
        YamlStream stream = LoadStream(reader, filePath);
        return ProjectRoot(stream.Documents[0].RootNode, filePath);
    }

    /// <summary>
    /// Lower-level shared parse used by both the loader (this class)
    /// and the validator (<c>FhirConnectValidator</c>): parse one YAML
    /// file, return the raw <see cref="YamlMappingNode"/> root and
    /// (separately) the typed projection. The validator runs schema
    /// evaluation against the raw tree, then uses the typed projection
    /// for semantic checks; the loader needs only the projection.
    /// </summary>
    /// <param name="filePath">Absolute file path. Used both to open the
    /// file and to seed the resulting <c>FhirConnectFormatException</c>
    /// messages.</param>
    /// <returns>The raw root mapping node and the typed mapping
    /// record. The typed projection is <c>null</c> when the grammar
    /// declaration is unsupported — callers that only need the raw
    /// tree (schema evaluation) can still proceed.</returns>
    [RequiresUnreferencedCode("See ReadFile.")]
    internal static (YamlMappingNode Root, FhirConnectMapping? Projection) ReadDocument(string filePath)
    {
        using StreamReader sr = new StreamReader(filePath);
        YamlStream stream = LoadStream(sr, filePath);
        YamlMappingNode root = ExpectMapping(stream.Documents[0].RootNode, filePath, "<root>");

        // The validator wants the raw tree even when the semantic
        // projection fails (e.g., missing required spec key). Surface
        // the projection failure to callers via a null projection — the
        // validator re-parses semantic shape from the raw tree and the
        // loader's public ReadFile / FhirConnectMapping.Load throws
        // the exception directly.
        FhirConnectMapping? projection;
        try
        {
            projection = ProjectRoot(root, filePath);
        }
        catch (FhirConnectFormatException)
        {
            projection = null;
        }
        return (root, projection);
    }

    private static YamlStream LoadStream(TextReader reader, string filePath)
    {
        YamlStream stream = new YamlStream();
        try
        {
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new FhirConnectFormatException(
                $"YAML parse error in '{filePath}': {ex.Message}",
                filePath,
                ex);
        }

        if (stream.Documents.Count == 0)
        {
            throw new FhirConnectFormatException(
                $"Empty YAML stream in '{filePath}'.", filePath);
        }
        return stream;
    }

    private static FhirConnectMapping ProjectRoot(YamlNode rootNode, string filePath)
    {
        YamlMappingNode root = ExpectMapping(rootNode, filePath, "<root>");

        string grammarString = RequireScalar(root, "grammar", filePath);
        if (!string.Equals(grammarString, SupportedGrammar, StringComparison.Ordinal))
        {
            throw new FhirConnectFormatException(
                $"Unsupported FHIRconnect grammar '{grammarString}' in '{filePath}'. Only '{SupportedGrammar}' is supported.",
                filePath,
                grammarString);
        }

        string typeString = RequireScalar(root, "type", filePath);
        MappingMetadata metadata = ReadMetadata(
            ExpectMapping(RequireChild(root, "metadata", filePath), filePath, "metadata"),
            filePath);
        MappingSpec spec = ReadSpec(
            ExpectMapping(RequireChild(root, "spec", filePath), filePath, "spec"),
            filePath,
            typeString);

        return typeString switch
        {
            "model" => ReadModel(root, metadata, spec, filePath),
            "context" => ReadContext(root, metadata, spec, filePath),
            "extension" => ReadExtension(root, metadata, spec, filePath),
            _ => throw new FhirConnectFormatException(
                $"Unsupported FHIRconnect mapping type '{typeString}' in '{filePath}'. Expected one of: model, context, extension.",
                filePath),
        };
    }

    private static ModelMapping ReadModel(
        YamlMappingNode root,
        MappingMetadata metadata,
        MappingSpec spec,
        string filePath)
    {
        IReadOnlyList<MappingRule> rules = ReadRuleList(
            root.Children.TryGetValue(new YamlScalarNode("mappings"), out YamlNode? node) ? node : null,
            filePath);
        return new ModelMapping(FhirConnectGrammar.V1_0_0, metadata, spec, rules);
    }

    private static ContextMapping ReadContext(
        YamlMappingNode root,
        MappingMetadata metadata,
        MappingSpec spec,
        string filePath)
    {
        YamlMappingNode ctx = ExpectMapping(
            RequireChild(root, "context", filePath), filePath, "context");

        YamlMappingNode profileNode = ExpectMapping(
            RequireChild(ctx, "profile", filePath), filePath, "context.profile");
        ProfileRef profile = new(
            RequireScalar(profileNode, "url", filePath),
            TryGetScalar(profileNode, "version"));

        YamlMappingNode templateNode = ExpectMapping(
            RequireChild(ctx, "template", filePath), filePath, "context.template");
        TemplateRef template = new(
            RequireScalar(templateNode, "id", filePath),
            TryGetScalar(templateNode, "sem_ver"));

        IReadOnlyList<string> archetypes = ReadStringList(ctx, "archetypes");
        IReadOnlyList<string> extensions = ReadStringList(ctx, "extensions");
        string start = RequireScalar(ctx, "start", filePath);

        ContextSpec contextSpec = new(profile, template, archetypes, extensions, start);
        return new ContextMapping(FhirConnectGrammar.V1_0_0, metadata, spec, contextSpec);
    }

    private static ExtensionMapping ReadExtension(
        YamlMappingNode root,
        MappingMetadata metadata,
        MappingSpec spec,
        string filePath)
    {
        IReadOnlyList<MappingRule> rules = ReadRuleList(
            root.Children.TryGetValue(new YamlScalarNode("mappings"), out YamlNode? node) ? node : null,
            filePath);
        return new ExtensionMapping(FhirConnectGrammar.V1_0_0, metadata, spec, rules);
    }

    private static MappingMetadata ReadMetadata(YamlMappingNode node, string filePath)
    {
        return new MappingMetadata(
            RequireScalar(node, "name", filePath),
            RequireScalar(node, "version", filePath));
    }

    private static MappingSpec ReadSpec(YamlMappingNode node, string filePath, string mappingType)
    {
        string system = RequireScalar(node, "system", filePath);
        string version = RequireScalar(node, "version", filePath);
        FhirRelease release = version switch
        {
            "R4" => FhirRelease.R4,
            "R4B" => FhirRelease.R4B,
            "R5" => FhirRelease.R5,
            _ => throw new FhirConnectFormatException(
                $"Unsupported FHIR release '{version}' in spec block of '{filePath}'. Expected R4, R4B, or R5.",
                filePath),
        };

        string? extends = TryGetScalar(node, "extends");
        OpenEhrConfig? openEhrConfig = null;
        if (node.Children.TryGetValue(new YamlScalarNode("openEhrConfig"), out YamlNode? oe))
        {
            YamlMappingNode oeMap = ExpectMapping(oe, filePath, "spec.openEhrConfig");
            openEhrConfig = new OpenEhrConfig(
                RequireScalar(oeMap, "archetype", filePath),
                TryGetScalar(oeMap, "revision"));
        }

        FhirConfig? fhirConfig = null;
        if (node.Children.TryGetValue(new YamlScalarNode("fhirConfig"), out YamlNode? fc))
        {
            YamlMappingNode fcMap = ExpectMapping(fc, filePath, "spec.fhirConfig");
            fhirConfig = new FhirConfig(
                RequireScalar(fcMap, "structureDefinition", filePath));
        }

        return new MappingSpec(system, release, extends, openEhrConfig, fhirConfig);
    }

    private static IReadOnlyList<MappingRule> ReadRuleList(YamlNode? listNode, string filePath)
    {
        if (listNode is null)
        {
            return [];
        }

        if (listNode is not YamlSequenceNode seq)
        {
            throw new FhirConnectFormatException(
                $"Expected sequence for 'mappings' in '{filePath}', got {listNode.NodeType}.",
                filePath);
        }

        List<MappingRule> rules = new List<MappingRule>(seq.Children.Count);
        foreach (YamlNode child in seq.Children)
        {
            rules.Add(ReadRule(ExpectMapping(child, filePath, "mappings[*]"), filePath));
        }
        return rules;
    }

    private static MappingRule ReadRule(YamlMappingNode node, string filePath)
    {
        string name = RequireScalar(node, "name", filePath);

        WithBlock with;
        if (node.Children.TryGetValue(new YamlScalarNode("with"), out YamlNode? withNode))
        {
            YamlMappingNode withMap = ExpectMapping(withNode, filePath, $"mappings[{name}].with");
            string? fhir = TryGetScalar(withMap, "fhir");
            string? openEhr = TryGetScalar(withMap, "openehr");
            string? typeStr = TryGetScalar(withMap, "type");
            WithType withType = typeStr switch
            {
                null => WithType.Default,
                "NONE" => WithType.None,
                _ => throw new FhirConnectFormatException(
                    $"Unsupported 'with.type' value '{typeStr}' on rule '{name}' in '{filePath}'. Expected 'NONE' or omitted.",
                    filePath),
            };
            with = new WithBlock(fhir, openEhr, withType);
        }
        else
        {
            // A rule without a `with:` block is rare but legal in the v1.0.0
            // grammar (purely a marker for a referenced rule name). Represent
            // as an empty block; the engine treats it as a no-op.
            with = new WithBlock(null, null, WithType.Default);
        }

        string? unidirectional = TryGetScalar(node, "unidirectional");

        IReadOnlyList<ManualEntry>? manual = null;
        if (node.Children.TryGetValue(new YamlScalarNode("manual"), out YamlNode? manualNode))
        {
            manual = ReadManual(manualNode, filePath, name);
        }

        FollowedBy? followedBy = null;
        if (node.Children.TryGetValue(new YamlScalarNode("followedBy"), out YamlNode? fbNode))
        {
            YamlMappingNode fbMap = ExpectMapping(fbNode, filePath, $"mappings[{name}].followedBy");
            followedBy = new FollowedBy(ReadRuleList(
                fbMap.Children.TryGetValue(new YamlScalarNode("mappings"), out YamlNode? fbList) ? fbList : null,
                filePath));
        }

        LinkSpec? link = null;
        if (node.Children.TryGetValue(new YamlScalarNode("link"), out YamlNode? linkNode))
        {
            YamlMappingNode linkMap = ExpectMapping(linkNode, filePath, $"mappings[{name}].link");
            link = new LinkSpec(
                RequireScalar(linkMap, "meaning", filePath),
                RequireScalar(linkMap, "type", filePath));
        }

        ReferenceSpec? reference = null;
        if (node.Children.TryGetValue(new YamlScalarNode("reference"), out YamlNode? refNode))
        {
            YamlMappingNode refMap = ExpectMapping(refNode, filePath, $"mappings[{name}].reference");
            string? resourceType = TryGetScalar(refMap, "resourceType");
            IReadOnlyList<MappingRule> nestedRules = ReadRuleList(
                refMap.Children.TryGetValue(new YamlScalarNode("mappings"), out YamlNode? refList) ? refList : null,
                filePath);
            reference = new ReferenceSpec(resourceType, nestedRules);
        }

        string? slotArchetype = TryGetScalar(node, "slotArchetype");

        ExtensionAction? extension = null;
        if (node.Children.TryGetValue(new YamlScalarNode("extension"), out YamlNode? extNode))
        {
            string extStr = (extNode as YamlScalarNode)?.Value
                ?? throw new FhirConnectFormatException(
                    $"Expected scalar for 'extension' on rule '{name}' in '{filePath}'.",
                    filePath);
            extension = extStr switch
            {
                "add" => ExtensionAction.Add,
                "overwrite" => ExtensionAction.Overwrite,
                "remove" => ExtensionAction.Remove,
                _ => throw new FhirConnectFormatException(
                    $"Unsupported 'extension' verb '{extStr}' on rule '{name}' in '{filePath}'. Expected one of: add, overwrite, remove.",
                    filePath),
            };
        }

        string? fhirCondition = TryGetScalar(node, "fhirCondition");

        MappingRule rule = new MappingRule(
            name,
            with,
            unidirectional,
            manual,
            followedBy,
            link,
            reference,
            slotArchetype,
            extension,
            fhirCondition);

        IReadOnlyList<string> ambiguous = RuleClassifier.AmbiguousPrimarySlots(rule);
        if (ambiguous.Count > 1)
        {
            throw new FhirConnectFormatException(
                $"Rule '{name}' in '{filePath}' carries ambiguous primary slots [{string.Join(", ", ambiguous)}]. Each rule may carry at most one of: reference, link, manual, followedBy (with type:NONE).",
                filePath);
        }

        return rule;
    }

    private static IReadOnlyList<ManualEntry> ReadManual(YamlNode node, string filePath, string ruleName)
    {
        if (node is not YamlSequenceNode seq)
        {
            throw new FhirConnectFormatException(
                $"Expected sequence for 'manual' on rule '{ruleName}' in '{filePath}'.",
                filePath);
        }

        List<ManualEntry> entries = new List<ManualEntry>(seq.Children.Count);
        foreach (YamlNode child in seq.Children)
        {
            YamlMappingNode entry = ExpectMapping(child, filePath, $"manual[*] under rule '{ruleName}'");
            string entryName = RequireScalar(entry, "name", filePath);
            IReadOnlyList<ManualField>? fhir = null;
            IReadOnlyList<ManualField>? openEhr = null;
            if (entry.Children.TryGetValue(new YamlScalarNode("fhir"), out YamlNode? fhirList))
            {
                fhir = ReadManualFields(fhirList, filePath, ruleName, "fhir");
            }
            if (entry.Children.TryGetValue(new YamlScalarNode("openehr"), out YamlNode? openList))
            {
                openEhr = ReadManualFields(openList, filePath, ruleName, "openehr");
            }
            entries.Add(new ManualEntry(entryName, fhir, openEhr));
        }
        return entries;
    }

    private static IReadOnlyList<ManualField> ReadManualFields(
        YamlNode node, string filePath, string ruleName, string side)
    {
        if (node is not YamlSequenceNode seq)
        {
            throw new FhirConnectFormatException(
                $"Expected sequence for 'manual.{side}' on rule '{ruleName}' in '{filePath}'.",
                filePath);
        }

        List<ManualField> fields = new List<ManualField>(seq.Children.Count);
        foreach (YamlNode child in seq.Children)
        {
            YamlMappingNode entry = ExpectMapping(
                child, filePath, $"manual.{side}[*] under rule '{ruleName}'");
            fields.Add(new ManualField(
                RequireScalar(entry, "path", filePath),
                RequireScalar(entry, "value", filePath)));
        }
        return fields;
    }

    private static IReadOnlyList<string> ReadStringList(YamlMappingNode node, string key)
    {
        if (!node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return [];
        }
        if (child is not YamlSequenceNode seq)
        {
            return [];
        }
        List<string> result = new List<string>(seq.Children.Count);
        foreach (YamlNode item in seq.Children)
        {
            if (item is YamlScalarNode scalar && scalar.Value is not null)
            {
                result.Add(scalar.Value);
            }
        }
        return result;
    }

    private static YamlMappingNode ExpectMapping(YamlNode node, string filePath, string label)
    {
        if (node is YamlMappingNode map)
        {
            return map;
        }
        throw new FhirConnectFormatException(
            $"Expected YAML mapping at '{label}' in '{filePath}', got {node.NodeType}.",
            filePath);
    }

    private static YamlNode RequireChild(YamlMappingNode node, string key, string filePath)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child))
        {
            return child;
        }
        throw new FhirConnectFormatException(
            $"Missing required '{key}' field in '{filePath}'.", filePath);
    }

    private static string RequireScalar(YamlMappingNode node, string key, string filePath)
    {
        YamlNode child = RequireChild(node, key, filePath);
        if (child is YamlScalarNode scalar && scalar.Value is not null)
        {
            return scalar.Value;
        }
        throw new FhirConnectFormatException(
            $"Expected scalar '{key}' in '{filePath}', got {child.NodeType}.", filePath);
    }

    private static string? TryGetScalar(YamlMappingNode node, string key)
    {
        if (node.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? child) &&
            child is YamlScalarNode scalar)
        {
            return scalar.Value;
        }
        return null;
    }
}
