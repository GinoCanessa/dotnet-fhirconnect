using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DotnetFhirConnect.Internal;

/// <summary>
/// Converts a YamlDotNet representation tree into an STJ
/// <see cref="JsonNode"/> tree, alongside an index that maps
/// RFC-6901 JSON pointers back to the originating YAML
/// <see cref="Mark"/> (line / column). The validator uses the
/// JsonNode tree to drive <c>JsonSchema.Net</c> and the mark index
/// to attach source locations to issues.
/// </summary>
internal sealed class YamlJsonBridge
{
    private readonly Dictionary<string, Mark> _marksByPointer = new();
    private readonly StringBuilder _pointer = new();

    private YamlJsonBridge()
    {
    }

    /// <summary>
    /// Convert <paramref name="root"/> to <see cref="JsonNode"/> form.
    /// </summary>
    /// <returns>The JsonNode root, plus a mark index for source-locating
    /// every pointer the conversion produced.</returns>
    [RequiresUnreferencedCode(
        "Uses YamlDotNet's representation model. Wrapped here so AOT-trim warnings "
        + "stay localized to the validator/loader call sites.")]
    public static (JsonNode? Node, IReadOnlyDictionary<string, Mark> MarkIndex) Convert(YamlNode root)
    {
        YamlJsonBridge bridge = new YamlJsonBridge();
        JsonNode? node = bridge.ConvertNode(root);
        return (node, bridge._marksByPointer);
    }

    private JsonNode? ConvertNode(YamlNode node)
    {
        _marksByPointer[_pointer.ToString()] = node.Start;
        switch (node)
        {
            case YamlScalarNode scalar:
                return ConvertScalar(scalar);
            case YamlMappingNode map:
                return ConvertMapping(map);
            case YamlSequenceNode seq:
                return ConvertSequence(seq);
            default:
                return null;
        }
    }

    private static JsonNode? ConvertScalar(YamlScalarNode scalar)
    {
        if (scalar.Value is null)
        {
            return null;
        }

        // Tag-driven type inference where the document is explicit.
        string? tag = scalar.Tag.IsEmpty ? null : scalar.Tag.Value;
        if (string.Equals(tag, "tag:yaml.org,2002:null", System.StringComparison.Ordinal))
        {
            return null;
        }
        if (string.Equals(tag, "tag:yaml.org,2002:bool", System.StringComparison.Ordinal) &&
            bool.TryParse(scalar.Value, out bool b))
        {
            return JsonValue.Create(b);
        }
        if (string.Equals(tag, "tag:yaml.org,2002:int", System.StringComparison.Ordinal) &&
            long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long i))
        {
            return JsonValue.Create(i);
        }
        if (string.Equals(tag, "tag:yaml.org,2002:float", System.StringComparison.Ordinal) &&
            double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            return JsonValue.Create(d);
        }

        // Plain scalar (no quoting in source) — infer cheaply so the
        // schema can apply numeric / boolean constraints. Quoted scalars
        // are preserved as strings.
        if (scalar.Style == ScalarStyle.Plain)
        {
            if (string.Equals(scalar.Value, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                return JsonValue.Create(true);
            }
            if (string.Equals(scalar.Value, "false", System.StringComparison.OrdinalIgnoreCase))
            {
                return JsonValue.Create(false);
            }
            if (string.Equals(scalar.Value, "null", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scalar.Value, "~", System.StringComparison.Ordinal) ||
                scalar.Value.Length == 0)
            {
                return null;
            }
            if (long.TryParse(scalar.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long pi))
            {
                return JsonValue.Create(pi);
            }
            if (double.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pd))
            {
                return JsonValue.Create(pd);
            }
        }
        return JsonValue.Create(scalar.Value);
    }

    private JsonObject ConvertMapping(YamlMappingNode map)
    {
        JsonObject obj = new JsonObject();
        int baseLen = _pointer.Length;
        foreach (KeyValuePair<YamlNode, YamlNode> kv in map.Children)
        {
            string key = kv.Key is YamlScalarNode keyScalar && keyScalar.Value is not null
                ? keyScalar.Value
                : kv.Key.ToString() ?? string.Empty;
            string encoded = EncodeJsonPointerSegment(key);
            _pointer.Append('/').Append(encoded);
            JsonNode? child = ConvertNode(kv.Value);
            obj[key] = child;
            _pointer.Length = baseLen;
        }
        return obj;
    }

    private JsonArray ConvertSequence(YamlSequenceNode seq)
    {
        JsonArray arr = new JsonArray();
        int baseLen = _pointer.Length;
        int idx = 0;
        foreach (YamlNode child in seq.Children)
        {
            _pointer.Append('/').Append(idx.ToString(CultureInfo.InvariantCulture));
            JsonNode? converted = ConvertNode(child);
            arr.Add(converted);
            _pointer.Length = baseLen;
            idx++;
        }
        return arr;
    }

    private static string EncodeJsonPointerSegment(string segment)
    {
        // RFC-6901: '~' -> '~0' first, then '/' -> '~1'.
        if (segment.IndexOf('~') < 0 && segment.IndexOf('/') < 0)
        {
            return segment;
        }
        return segment.Replace("~", "~0").Replace("/", "~1");
    }
}
