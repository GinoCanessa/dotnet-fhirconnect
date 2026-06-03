using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using DotnetFhirConnect.Validation;

namespace DotnetFhirConnect.Cli.Verbs;

internal static class ValidateVerb
{
    [RequiresUnreferencedCode(
        "Routes through FhirConnectValidator; library is not AOT-publishable in v0.x.")]
    public static int Run(string mappingPath, string format, TextWriter output, TextWriter error)
    {
        if (string.IsNullOrWhiteSpace(mappingPath))
        {
            error.WriteLine("validate: --mapping is required.");
            return ExitCodes.UsageError;
        }
        if (!File.Exists(mappingPath) && !Directory.Exists(mappingPath))
        {
            error.WriteLine($"validate: path not found: '{mappingPath}'.");
            return ExitCodes.IoOrParseError;
        }

        ValidationReport report;
        try
        {
            report = FhirConnectValidator.Validate(mappingPath);
        }
        catch (Exception ex)
        {
            error.WriteLine($"validate: {ex.Message}");
            return ExitCodes.IoOrParseError;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            WriteJsonReport(report, output);
        }
        else
        {
            WriteTextReport(report, output);
        }

        return report.IsValid ? ExitCodes.Success : ExitCodes.ValidationFailed;
    }

    private static void WriteTextReport(ValidationReport report, TextWriter output)
    {
        if (report.IsValid)
        {
            output.WriteLine("Valid.");
            return;
        }
        output.WriteLine($"Invalid — {report.Issues.Count} issue(s):");
        foreach (ValidationIssue issue in report.Issues)
        {
            string locator = issue.Line is int l ? $":{l}" : string.Empty;
            output.WriteLine($"  [{issue.Severity} {issue.Code}] {issue.FilePath}{locator} {issue.Pointer} — {issue.Message}");
        }
    }

    private static void WriteJsonReport(ValidationReport report, TextWriter output)
    {
        // Manual JSON building keeps the verb AOT-clean (no reflection-driven
        // serialization). The report shape is small and stable.
        StringBuilder sb = new StringBuilder();
        sb.Append("{\n  \"isValid\": ").Append(report.IsValid ? "true" : "false").Append(",\n");
        sb.Append("  \"issues\": [");
        for (int i = 0; i < report.Issues.Count; i++)
        {
            ValidationIssue issue = report.Issues[i];
            sb.Append(i == 0 ? "\n    " : ",\n    ");
            sb.Append("{\n      \"filePath\": ").Append(EncodeJsonString(issue.FilePath)).Append(",\n");
            sb.Append("      \"pointer\": ").Append(EncodeJsonString(issue.Pointer)).Append(",\n");
            sb.Append("      \"line\": ").Append(issue.Line?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null").Append(",\n");
            sb.Append("      \"column\": ").Append(issue.Column?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null").Append(",\n");
            sb.Append("      \"severity\": ").Append(EncodeJsonString(issue.Severity.ToString())).Append(",\n");
            sb.Append("      \"code\": ").Append(EncodeJsonString(issue.Code)).Append(",\n");
            sb.Append("      \"message\": ").Append(EncodeJsonString(issue.Message)).Append("\n    }");
        }
        if (report.Issues.Count > 0) { sb.Append("\n  "); }
        sb.Append("]\n}");
        output.WriteLine(sb.ToString());
    }

    private static string EncodeJsonString(string? s)
    {
        if (s is null) { return "null"; }
        StringBuilder sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) { sb.Append("\\u").Append(((int)ch).ToString("x4")); }
                    else { sb.Append(ch); }
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
