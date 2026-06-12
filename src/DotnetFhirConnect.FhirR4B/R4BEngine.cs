using System.Diagnostics.CodeAnalysis;
using DotnetFhirConnect.Mappings;
using Hl7.Fhir.Model;
using OpenEhrComposition = DotnetOpenEhr.Rm.Composition.Composition;

namespace DotnetFhirConnect.Fhir.R4B;

/// <summary>
/// Typed convenience facade over <see cref="FhirConnectEngine"/>
/// for R4B callers. Returns <c>Hl7.Fhir.R4B.Model.Resource</c>
/// directly instead of the engine's <see cref="object"/> seam.
/// </summary>
public sealed class R4BEngine
{
    private readonly FhirConnectEngine _core;

    /// <summary>
    /// Initialize the R4B facade with a pre-loaded mapping bundle.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">If the
    /// bundle's chosen model mapping is not targeted at R4B.</exception>
    public R4BEngine(MappingBundle bundle)
    {
        _core = new FhirConnectEngine(bundle);
        if (_core.Adapter.Release != FhirRelease.R4B)
        {
            throw new System.InvalidOperationException(
                $"R4BEngine: bundle's model mapping targets {_core.Adapter.Release}, not R4B. " +
                "Use R4Engine / R5Engine or the release-agnostic FhirConnectEngine.");
        }
    }

    /// <summary>The core engine this facade delegates to.</summary>
    public FhirConnectEngine Core => _core;

    /// <summary>
    /// openEHR Composition → typed R4B <see cref="Resource"/>.
    /// </summary>
    [RequiresUnreferencedCode("See FhirConnectEngine.")]
    public Resource ToFhir(OpenEhrComposition composition)
    {
        return (Resource)_core.ToFhir(composition);
    }
}
