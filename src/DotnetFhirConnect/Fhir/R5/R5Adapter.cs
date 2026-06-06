using System;
using System.Diagnostics.CodeAnalysis;

namespace DotnetFhirConnect.Fhir.R5;

/// <summary>
/// HL7 FHIR R5 implementation of <see cref="IFhirAdapter"/>. See
/// <c>R4Adapter</c> for the contract — the only difference is the
/// per-release <c>R5ObservationShim</c> and the <see cref="Release"/>
/// enum value. All shared logic lives in
/// <see cref="AdapterCore"/>.
/// </summary>
public sealed class R5Adapter : IFhirAdapter
{
    private readonly IObservationShim _shim = new R5ObservationShim();
    private const string AdapterDisplayName = "R5Adapter";

    /// <inheritdoc/>
    public FhirRelease Release => FhirRelease.R5;

    /// <inheritdoc/>
    [RequiresUnreferencedCode(
        "Hl7.Fhir.R5 parsers traverse the typed POCO graph and are not currently AOT-clean. "
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
                $"R5Adapter.CreateResource: type '{typeName}' not registered in v0.x. " +
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
