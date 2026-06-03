using System;
using System.Diagnostics.CodeAnalysis;

namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Internal seam through which the engine reads and writes FHIR
/// resources for a specific release (R4 / R4B / R5). Implementations
/// traffic in <see cref="object"/> for resources because the three
/// Firely packages each define their own <c>Resource</c> base type
/// with no shared assembly; typed access is provided by per-version
/// facades that layer on top of the engine (see Phase 6a).
/// </summary>
public interface IFhirAdapter
{
    /// <summary>The FHIR release this adapter is bound to.</summary>
    FhirRelease Release { get; }

    /// <summary>
    /// Deserialize a FHIR resource from a JSON character span.
    /// </summary>
    /// <returns>A boxed <c>Hl7.Fhir.{R4|R4B|R5}.Model.Resource</c>.</returns>
    [RequiresUnreferencedCode(
        "Firely SDK serializers are not currently AOT-clean. v0.x is not AOT-publishable.")]
    object ParseResource(ReadOnlySpan<char> json);

    /// <summary>
    /// Serialize a FHIR resource to its canonical JSON form.
    /// </summary>
    [RequiresUnreferencedCode("See ParseResource.")]
    string SerializeResource(object resource);

    /// <summary>
    /// Construct an empty FHIR resource of the named type
    /// (e.g. <c>"Observation"</c>).
    /// </summary>
    /// <exception cref="System.ArgumentException">If the type name is
    /// not recognised by the adapter's release.</exception>
    object CreateResource(string typeName);

    /// <summary>
    /// Set <paramref name="value"/> at <paramref name="path"/> on
    /// <paramref name="resource"/>. The adapter understands a
    /// restricted subset of FHIRPath-style accessors — single-segment
    /// dotted property paths only (e.g. <c>"Observation.note.text"</c>).
    /// </summary>
    /// <param name="resource">The resource to mutate.</param>
    /// <param name="path">The dotted property path. The
    /// <c>$resource.</c> prefix from FHIRconnect rule paths is
    /// stripped by the caller, not by the adapter.</param>
    /// <param name="value">The value to assign. Adapter-specific
    /// type coercion may apply (e.g. raw <c>string</c> wrapped in a
    /// FHIR <c>Markdown</c> for <c>note.text</c>).</param>
    /// <param name="error">When the call returns <c>false</c>, a
    /// human-readable diagnostic explaining why.</param>
    /// <returns><c>true</c> on success; <c>false</c> if the path is
    /// unknown or the value is incompatible. Never throws on bad
    /// input.</returns>
    bool TrySetValue(
        object resource,
        string path,
        object? value,
        [NotNullWhen(false)] out string? error);

    /// <summary>
    /// Read the value at <paramref name="path"/> on
    /// <paramref name="resource"/>. Same path syntax as
    /// <see cref="TrySetValue"/>.
    /// </summary>
    /// <returns><c>true</c> when the value is present and resolvable;
    /// <c>false</c> when the path is unknown or no value is set.</returns>
    bool TryGetValue(object resource, string path, out object? value);
}
