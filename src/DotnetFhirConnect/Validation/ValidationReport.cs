namespace DotnetFhirConnect.Validation;

/// <summary>
/// One issue raised by <see cref="FhirConnectValidator.Validate(string)"/>
/// against a mapping file.
/// </summary>
/// <param name="FilePath">Absolute path to the file the issue applies to.</param>
/// <param name="Pointer">RFC-6901 JSON pointer rooted at the YAML document
/// (e.g. <c>"/spec/version"</c>, <c>"/mappings/3/link/type"</c>).</param>
/// <param name="Line">1-based source line number, best-effort. Null when
/// the pointer cannot be mapped back to a YAML mark.</param>
/// <param name="Column">1-based column. Null when not available.</param>
/// <param name="Severity">Whether this is an error or a warning.</param>
/// <param name="Code">Stable issue code (e.g. <c>"FCV001"</c>) usable as
/// a filter key.</param>
/// <param name="Message">Human-readable description.</param>
public sealed record ValidationIssue(
    string FilePath,
    string Pointer,
    int? Line,
    int? Column,
    ValidationSeverity Severity,
    string Code,
    string Message);

/// <summary>
/// Aggregate result of validating one file (or one directory) against
/// the FHIRconnect v1.0.0 spec.
/// </summary>
/// <param name="IsValid">True iff <see cref="Issues"/> contains no
/// <see cref="ValidationSeverity.Error"/> entries.</param>
/// <param name="Issues">All errors and warnings raised during validation,
/// in discovery order.</param>
public sealed record ValidationReport(bool IsValid, IReadOnlyList<ValidationIssue> Issues);
