using System;
using System.Diagnostics.CodeAnalysis;

namespace DotnetFhirConnect.Fhir.R4;

/// <summary>
/// HL7 FHIR R4 implementation of <see cref="IFhirAdapter"/>. The
/// release-specific surface is intentionally tiny: parse / serialize
/// plumbing plus the per-release <c>R4ObservationShim</c>. All
/// path-handling logic lives in <see cref="AdapterCore"/> and is
/// shared with the R4B / R5 adapters; see <see cref="AdapterCore"/>
/// for the contract on adding a new release.
/// </summary>
public sealed class R4Adapter : IFhirAdapter
{
    private readonly IObservationShim _shim = new R4ObservationShim();
    private const string AdapterDisplayName = "R4Adapter";

    /// <inheritdoc/>
    public FhirRelease Release => FhirRelease.R4;

    /// <inheritdoc/>
    [RequiresUnreferencedCode(
        "Hl7.Fhir.R4 parsers traverse the typed POCO graph and are not currently AOT-clean. "
        + "Library is not AOT-publishable in v0.x.")]
    public object ParseResource(ReadOnlySpan<char> json) => _shim.ParseResource(json);

    /// <inheritdoc/>
    [RequiresUnreferencedCode("See ParseResource.")]
    public string SerializeResource(object resource) => _shim.SerializeResource(resource);

    /// <inheritdoc/>
    public object CreateResource(string typeName)
    {
        return typeName switch
        {
            "Observation" => _shim.CreateObservation(),
            _ => throw new ArgumentException(
                $"R4Adapter.CreateResource: type '{typeName}' not registered in v0.x. " +
                "Add the type to the per-release shim (and AdapterCore dispatch) to broaden coverage.",
                nameof(typeName)),
        };
    }

    /// <inheritdoc/>
    public bool TrySetValue(
        object resource,
        string path,
        object? value,
        [NotNullWhen(false)] out string? error) =>
        AdapterCore.TrySetValue(_shim, AdapterDisplayName, resource, path, value, out error);

    /// <inheritdoc/>
    public bool TryGetValue(object resource, string path, out object? value) =>
        AdapterCore.TryGetValue(_shim, resource, path, out value);
}
