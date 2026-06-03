using System;

namespace DotnetFhirConnect.Mappings;

/// <summary>
/// Thrown by <see cref="FhirConnectMapping.Load(string)"/> when a
/// vendored or user-supplied mapping file cannot be parsed under the
/// supported grammar (v1.0.0).
/// </summary>
public sealed class FhirConnectFormatException : Exception
{
    /// <summary>The file the loader was reading when it failed.</summary>
    public string? FilePath { get; }

    /// <summary>Optional grammar string the file declared.</summary>
    public string? GrammarString { get; }

    /// <summary>
    /// Default constructor — used when neither file path nor grammar
    /// are known at throw time.
    /// </summary>
    public FhirConnectFormatException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructor that records the offending file path.
    /// </summary>
    public FhirConnectFormatException(string message, string filePath)
        : base(message)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Constructor that records both the file path and the rejected
    /// grammar declaration. Both are included in the message.
    /// </summary>
    public FhirConnectFormatException(string message, string filePath, string grammarString)
        : base(message)
    {
        FilePath = filePath;
        GrammarString = grammarString;
    }

    /// <summary>
    /// Constructor with an inner exception (typically the YAML
    /// parser's own diagnostic).
    /// </summary>
    public FhirConnectFormatException(string message, string filePath, Exception inner)
        : base(message, inner)
    {
        FilePath = filePath;
    }
}
