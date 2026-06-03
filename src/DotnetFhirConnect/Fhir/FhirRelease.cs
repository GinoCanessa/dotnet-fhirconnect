namespace DotnetFhirConnect.Fhir;

/// <summary>
/// Supported HL7 FHIR releases. Drives selection of the
/// <see cref="IFhirAdapter"/> implementation a mapping bundle uses
/// at engine-construction time.
/// </summary>
public enum FhirRelease
{
    /// <summary>HL7 FHIR R4.</summary>
    R4,

    /// <summary>HL7 FHIR R4B.</summary>
    R4B,

    /// <summary>HL7 FHIR R5.</summary>
    R5,
}
