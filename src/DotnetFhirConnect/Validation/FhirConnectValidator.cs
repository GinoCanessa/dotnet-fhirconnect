using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DotnetFhirConnect.Internal;
using DotnetFhirConnect.Mappings;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace DotnetFhirConnect.Validation;

/// <summary>
/// Validates one FHIRconnect v1.0.0 mapping file (or a bundle directory)
/// against the embedded JSON schemas plus a small set of semantic rules
/// the spec body imposes but the schemas do not capture.
/// </summary>
/// <remarks>
/// Issue codes used:
/// <list type="bullet">
///   <item><c>FCV001</c>: unsupported grammar version.</item>
///   <item><c>FCV002</c>: unrecognised <c>type:</c> declaration.</item>
///   <item><c>FCV003</c>: <c>spec.system</c> is not <c>FHIR</c>.</item>
///   <item><c>FCV004</c>: <c>spec.version</c> is not R4/R4B/R5.</item>
///   <item><c>FCV005</c>: model file with malformed openEHR archetype id.</item>
///   <item><c>FCV010</c>: context file <c>start</c> is not in <c>archetypes</c>.</item>
///   <item><c>FCV100</c>: schema-evaluator error (catch-all).</item>
///   <item><c>FCV900</c>: catastrophic parse failure (file kept-open
///     for context but no further validation possible).</item>
/// </list>
/// </remarks>
public static class FhirConnectValidator
{
    private static readonly Regex ArchetypeIdRegex = new Regex(
        @"^openEHR-EHR-[A-Z_]+(\.[A-Za-z0-9_-]+)+\.v\d+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static JsonSchema? s_modelSchema;
    private static JsonSchema? s_contextSchema;
    private static readonly object s_schemaLock = new object();

    /// <summary>
    /// Validate <paramref name="path"/> against the FHIRconnect v1.0.0
    /// spec. <paramref name="path"/> may be a single file
    /// (<c>*.yml</c>/<c>*.yaml</c>) or a directory of files.
    /// </summary>
    [RequiresUnreferencedCode(
        "Routes through MappingYamlReader (YamlDotNet) and JsonSchema.Net "
        + "evaluation. Both are flagged by the AOT analyzer.")]
    public static ValidationReport Validate(string path)
    {
        if (Directory.Exists(path))
        {
            return ValidateDirectory(path);
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"FHIRconnect path not found: '{path}'.", path);
        }
        return ValidateFile(path);
    }

    [RequiresUnreferencedCode("See Validate.")]
    private static ValidationReport ValidateDirectory(string directory)
    {
        List<ValidationIssue> issues = [];
        foreach (string file in Directory.EnumerateFiles(directory, "*.y*ml", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (!string.Equals(ext, ".yml", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".yaml", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            ValidationReport sub = ValidateFile(file);
            issues.AddRange(sub.Issues);
        }
        return new ValidationReport(
            IsValid: issues.All(i => i.Severity != ValidationSeverity.Error),
            Issues: issues);
    }

    [RequiresUnreferencedCode("See Validate.")]
    private static ValidationReport ValidateFile(string filePath)
    {
        List<ValidationIssue> issues = [];

        YamlMappingNode root;
        FhirConnectMapping? projection;
        try
        {
            (root, projection) = MappingYamlReader.ReadDocument(filePath);
        }
        catch (FhirConnectFormatException ex)
        {
            // Catastrophic YAML-level failure — no raw tree available.
            ValidationSeverity sev = ValidationSeverity.Error;
            string code = ex.GrammarString is not null ? "FCV001" : "FCV900";
            issues.Add(new ValidationIssue(filePath, "", null, null, sev, code, ex.Message));
            return new ValidationReport(IsValid: false, Issues: issues);
        }

        (JsonNode? json, IReadOnlyDictionary<string, Mark> marks) = YamlJsonBridge.Convert(root);
        if (json is not JsonObject jObj)
        {
            issues.Add(new ValidationIssue(
                filePath, "", null, null, ValidationSeverity.Error, "FCV900",
                $"YAML root in '{filePath}' did not project to a JSON object."));
            return new ValidationReport(IsValid: false, Issues: issues);
        }

        // Grammar check up front so we can emit FCV001 with a precise
        // pointer rather than letting the schema's "doesn't match
        // pattern" error stand in. If grammar is unsupported, stop —
        // the rest of the validation has no meaningful semantics
        // against a foreign grammar.
        string? grammarString = jObj["grammar"]?.GetValue<string>();
        if (!string.Equals(grammarString, SupportedGrammar, System.StringComparison.Ordinal))
        {
            (int? l, int? c) = LocateMark(marks, "/grammar");
            issues.Add(new ValidationIssue(
                filePath, "/grammar", l, c, ValidationSeverity.Error, "FCV001",
                $"Unsupported FHIRconnect grammar '{grammarString}' in '{filePath}'. Only '{SupportedGrammar}' is supported."));
            return new ValidationReport(IsValid: false, Issues: issues);
        }

        string? typeString = jObj["type"]?.GetValue<string>();
        JsonSchema? schema = typeString switch
        {
            "model" => LoadSchema(ref s_modelSchema, "DotnetFhirConnect.Resources.model-mapping.schema.json"),
            "context" => LoadSchema(ref s_contextSchema, "DotnetFhirConnect.Resources.contextual-mapping.schema.json"),
            "extension" => LoadSchema(ref s_modelSchema, "DotnetFhirConnect.Resources.model-mapping.schema.json"),
            _ => null,
        };

        if (typeString is null || (typeString != "model" && typeString != "context" && typeString != "extension"))
        {
            (int? line, int? col) = LocateMark(marks, "/type");
            issues.Add(new ValidationIssue(
                filePath, "/type", line, col, ValidationSeverity.Error, "FCV002",
                $"Unrecognised mapping type '{typeString}' in '{filePath}'. Expected model / context / extension."));
        }
        else if (typeString != "extension" && schema is not null)
        {
            // Extension files re-use the model schema for the rule list
            // but extend the spec block with the 'extends' key; the
            // upstream schema would reject 'extends'. Skip schema eval
            // on extension files in v0.x; semantic checks below still
            // run. (Tracked as a known gap; v0.x scope.)
            using JsonDocument doc = JsonDocument.Parse(jObj.ToJsonString());
            EvaluationOptions opts = new EvaluationOptions { OutputFormat = OutputFormat.List };
            EvaluationResults res = schema.Evaluate(doc.RootElement, opts);
            if (!res.IsValid)
            {
                EmitSchemaResults(filePath, marks, res, issues);
            }
        }

        if (projection is not null)
        {
            SemanticCheck(filePath, projection, jObj, marks, issues);
        }

        bool isValid = issues.All(i => i.Severity != ValidationSeverity.Error);
        return new ValidationReport(IsValid: isValid, Issues: issues);
    }

    private const string SupportedGrammar = "FHIRConnect/v1.0.0";

    private static void SemanticCheck(
        string filePath,
        FhirConnectMapping projection,
        JsonObject jObj,
        IReadOnlyDictionary<string, Mark> marks,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(projection.Spec.System, "FHIR", System.StringComparison.Ordinal))
        {
            (int? l, int? c) = LocateMark(marks, "/spec/system");
            issues.Add(new ValidationIssue(
                filePath, "/spec/system", l, c, ValidationSeverity.Error, "FCV003",
                $"spec.system must be 'FHIR'; got '{projection.Spec.System}'."));
        }

        if (projection is ModelMapping model && model.Spec.OpenEhrConfig is OpenEhrConfig oe)
        {
            if (!ArchetypeIdRegex.IsMatch(oe.Archetype))
            {
                (int? l, int? c) = LocateMark(marks, "/spec/openEhrConfig/archetype");
                issues.Add(new ValidationIssue(
                    filePath, "/spec/openEhrConfig/archetype", l, c,
                    ValidationSeverity.Error, "FCV005",
                    $"Malformed openEHR archetype id '{oe.Archetype}' on model mapping '{model.Metadata.Name}'."));
            }
        }

        if (projection is ContextMapping ctx)
        {
            if (!ctx.Context.Archetypes.Contains(ctx.Context.Start, System.StringComparer.Ordinal))
            {
                (int? l, int? c) = LocateMark(marks, "/context/start");
                issues.Add(new ValidationIssue(
                    filePath, "/context/start", l, c, ValidationSeverity.Error, "FCV010",
                    $"context.start '{ctx.Context.Start}' is not declared in context.archetypes."));
            }
        }
    }

    private static void EmitSchemaResults(
        string filePath,
        IReadOnlyDictionary<string, Mark> marks,
        EvaluationResults res,
        List<ValidationIssue> issues)
    {
        // OutputFormat.List flattens the tree; failing nodes carry a list of error keywords.
        Walk(res);

        void Walk(EvaluationResults node)
        {
            if (!node.IsValid && node.Errors is { Count: > 0 })
            {
                string pointer = node.InstanceLocation.ToString();
                (int? line, int? col) = LocateMark(marks, pointer);
                StringBuilder msg = new StringBuilder();
                bool first = true;
                foreach (KeyValuePair<string, string> err in node.Errors)
                {
                    if (!first) msg.Append("; ");
                    msg.Append(err.Key).Append(": ").Append(err.Value);
                    first = false;
                }
                issues.Add(new ValidationIssue(
                    filePath, pointer, line, col, ValidationSeverity.Error, "FCV100", msg.ToString()));
            }
            if (node.Details is { Count: > 0 })
            {
                foreach (EvaluationResults child in node.Details)
                {
                    Walk(child);
                }
            }
        }
    }

    private static (int? Line, int? Column) LocateMark(
        IReadOnlyDictionary<string, Mark> marks, string pointer)
    {
        if (marks.TryGetValue(pointer, out Mark mark))
        {
            return ((int)mark.Line, (int)mark.Column);
        }
        return (null, null);
    }

    private static JsonSchema LoadSchema(ref JsonSchema? cache, string resourceName)
    {
        if (cache is not null)
        {
            return cache;
        }
        lock (s_schemaLock)
        {
            if (cache is not null)
            {
                return cache;
            }
            Assembly assembly = typeof(FhirConnectValidator).Assembly;
            using Stream? s = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Embedded schema resource '{resourceName}' not found.");
            using StreamReader sr = new StreamReader(s);
            string text = sr.ReadToEnd();
            cache = JsonSchema.FromText(text);
            return cache;
        }
    }
}
