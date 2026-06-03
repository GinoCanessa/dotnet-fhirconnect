using System;
using System.Diagnostics.CodeAnalysis;

namespace DotnetFhirConnect.Fhir.R5;

/// <summary>
/// Pending HL7 FHIR R5 adapter. v0.x ships the type so the seam is
/// exercised at engine-construction time, but every method throws
/// <see cref="NotImplementedException"/> with a release-tagged
/// message until a follow-on slot lands the typed coverage.
/// </summary>
public sealed class R5Adapter : IFhirAdapter
{
    /// <inheritdoc/>
    public FhirRelease Release => FhirRelease.R5;

    private const string PendingMessage =
        "FHIRconnect: R5 adapter pending. v0.x ships R4 only; track follow-on slot for R5.";

    /// <inheritdoc/>
    [RequiresUnreferencedCode(PendingMessage)]
    public object ParseResource(ReadOnlySpan<char> json) => throw new NotImplementedException(PendingMessage);

    /// <inheritdoc/>
    [RequiresUnreferencedCode(PendingMessage)]
    public string SerializeResource(object resource) => throw new NotImplementedException(PendingMessage);

    /// <inheritdoc/>
    public object CreateResource(string typeName) => throw new NotImplementedException(PendingMessage);

    /// <inheritdoc/>
    public bool TrySetValue(object resource, string path, object? value, [NotNullWhen(false)] out string? error)
        => throw new NotImplementedException(PendingMessage);

    /// <inheritdoc/>
    public bool TryGetValue(object resource, string path, out object? value)
        => throw new NotImplementedException(PendingMessage);
}
