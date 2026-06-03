using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Model;
using DotnetFhirConnect.Mappings;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

namespace DotnetFhirConnect.Fhir.R4;

/// <summary>
/// Typed convenience facade over <see cref="FhirConnectEngine"/>
/// for R4 callers. Returns <c>Hl7.Fhir.R4.Model.Resource</c>
/// directly instead of the engine's <see cref="object"/> seam.
/// </summary>
public sealed class R4Engine
{
    private readonly FhirConnectEngine _core;

    /// <summary>
    /// Initialize the R4 facade with a pre-loaded mapping bundle.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">If the
    /// bundle's chosen model mapping is not targeted at R4.</exception>
    public R4Engine(MappingBundle bundle)
    {
        _core = new FhirConnectEngine(bundle);
        if (_core.Adapter.Release != FhirRelease.R4)
        {
            throw new System.InvalidOperationException(
                $"R4Engine: bundle's model mapping targets {_core.Adapter.Release}, not R4. " +
                "Use R4BEngine / R5Engine (pending) or the release-agnostic FhirConnectEngine.");
        }
    }

    /// <summary>The core engine this facade delegates to.</summary>
    public FhirConnectEngine Core => _core;

    /// <summary>
    /// openEHR Composition → typed R4 <see cref="Resource"/>.
    /// </summary>
    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public Resource ToFhir(OpenEhrComposition composition)
    {
        return (Resource)_core.ToFhir(composition);
    }
}
