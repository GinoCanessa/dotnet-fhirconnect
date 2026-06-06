using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DotnetFhirConnect.Fhir;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Test-only decorator over an <see cref="IFhirAdapter"/> that
/// records every <see cref="IFhirAdapter.TrySetValue"/> call (the
/// path the executor handed to the adapter, the boxed resource's
/// runtime type name, and the boxed value) and forwards everything
/// else verbatim to the inner adapter. Used by Phase 1 dispatch
/// tests to assert the executor wrote the expected path-shape and
/// value-type into each rule arm without leaking adapter-internal
/// details into the assertions.
/// </summary>
internal sealed class RecordingFhirAdapter : IFhirAdapter
{
    private readonly IFhirAdapter _inner;
    private readonly List<Capture> _captures = [];

    public RecordingFhirAdapter(IFhirAdapter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>One captured <see cref="TrySetValue"/> invocation.</summary>
    public readonly record struct Capture(string ResourceType, string Path, object? Value);

    /// <summary>The captures recorded so far, in call order.</summary>
    public IReadOnlyList<Capture> Captures => _captures;

    /// <inheritdoc/>
    public FhirRelease Release => _inner.Release;

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Forwards to the inner IFhirAdapter.")]
    public object ParseResource(ReadOnlySpan<char> json) => _inner.ParseResource(json);

    /// <inheritdoc/>
    [RequiresUnreferencedCode("Forwards to the inner IFhirAdapter.")]
    public string SerializeResource(object resource) => _inner.SerializeResource(resource);

    /// <inheritdoc/>
    public object CreateResource(string typeName) => _inner.CreateResource(typeName);

    /// <inheritdoc/>
    public bool TrySetValue(
        object resource,
        string path,
        object? value,
        [NotNullWhen(false)] out string? error)
    {
        _captures.Add(new Capture(
            ResourceType: resource?.GetType().Name ?? "null",
            Path: path,
            Value: value));
        return _inner.TrySetValue(resource!, path, value, out error);
    }

    /// <inheritdoc/>
    public bool TryGetValue(object resource, string path, out object? value) =>
        _inner.TryGetValue(resource, path, out value);
}
