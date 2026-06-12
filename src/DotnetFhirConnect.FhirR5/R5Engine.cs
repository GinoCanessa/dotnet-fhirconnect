using System.Diagnostics.CodeAnalysis;
using DotnetFhirConnect.Mappings;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

namespace DotnetFhirConnect.Fhir.R5;

/// <summary>
/// Typed convenience facade over <see cref="FhirConnectEngine"/>
/// for R5 callers. Returns <c>Hl7.Fhir.R5.Model.Resource</c>
/// directly instead of the engine's <see cref="object"/> seam.
/// </summary>
public sealed class R5Engine
{
    private readonly FhirConnectEngine _core;

    /// <summary>
    /// Initialize the R5 facade with a pre-loaded mapping bundle.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">If the
    /// bundle's chosen model mapping is not targeted at R5.</exception>
    public R5Engine(MappingBundle bundle)
    {
        _core = new FhirConnectEngine(bundle);
        if (_core.Adapter.Release != FhirRelease.R5)
        {
            throw new System.InvalidOperationException(
                $"R5Engine: bundle's model mapping targets {_core.Adapter.Release}, not R5. " +
                "Use R4Engine / R4BEngine or the release-agnostic FhirConnectEngine.");
        }
    }

    /// <summary>The core engine this facade delegates to.</summary>
    public FhirConnectEngine Core => _core;

    /// <summary>
    /// openEHR Composition → typed R5 <see cref="Resource"/>.
    /// </summary>
    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public Resource ToFhir(OpenEhrComposition composition)
    {
        return (Resource)_core.ToFhir(composition);
    }
}
