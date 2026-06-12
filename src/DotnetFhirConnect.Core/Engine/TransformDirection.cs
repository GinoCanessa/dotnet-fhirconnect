namespace DotnetFhirConnect.Engine;

/// <summary>
/// Which way the engine is transforming. Drives <c>unidirectional</c>
/// rule filtering and <c>manual</c> entry side selection.
/// </summary>
internal enum TransformDirection
{
    /// <summary>openEHR → FHIR.</summary>
    ToFhir,

    /// <summary>FHIR → openEHR.</summary>
    ToOpenEhr,
}
