namespace DotnetFhirConnect.Validation;

/// <summary>
/// Severity classifier on a <see cref="ValidationIssue"/>. Validator
/// callers can choose to fail on Errors only, or to surface Warnings
/// too.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Best-practice / advisory; does not invalidate the file.</summary>
    Warning,

    /// <summary>Hard failure: the file is not valid v1.0.0.</summary>
    Error,
}
