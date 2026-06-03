namespace DotnetFhirConnect.Cli;

/// <summary>
/// CLI exit codes shared by <c>validate</c> and <c>transform</c>.
/// Mirrors <c>sysexits.h</c> conventions where possible.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Successful run.</summary>
    public const int Success = 0;

    /// <summary>Validation failed — the report has at least one error.</summary>
    public const int ValidationFailed = 1;

    /// <summary>I/O or parse error (file missing, malformed YAML/JSON).</summary>
    public const int IoOrParseError = 2;

    /// <summary>Transform / runtime error inside the engine.</summary>
    public const int TransformError = 3;

    /// <summary>Usage error — bad arguments, unknown verb, etc.</summary>
    public const int UsageError = 64;
}
