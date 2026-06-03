namespace DotnetFhirConnect.Mappings;

/// <summary>
/// Supported FHIRconnect grammar versions. Today only the v1.0.0
/// flavor parsed by <see cref="FhirConnectMapping.Load(string)"/>;
/// the enum exists to make the (current) "anything else is rejected"
/// behaviour explicit at call sites.
/// </summary>
public enum FhirConnectGrammar
{
    /// <summary>The <c>FHIRConnect/v1.0.0</c> grammar.</summary>
    V1_0_0,
}

/// <summary>
/// Three kinds of FHIRconnect mapping file the loader recognises:
/// the <see cref="Model"/> mapping (one per archetype), the
/// <see cref="Context"/> bundle (one per project), and the
/// <see cref="Extension"/> overlay (zero or more per project).
/// </summary>
public enum MappingType
{
    /// <summary>A <c>type: model</c> mapping file.</summary>
    Model,

    /// <summary>A <c>type: context</c> mapping file.</summary>
    Context,

    /// <summary>A <c>type: extension</c> mapping file.</summary>
    Extension,
}

/// <summary>
/// Supported HL7 FHIR releases the loader can encounter in a
/// mapping file's <c>spec.version</c>.
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

/// <summary>
/// The <c>type: NONE</c> marker that suppresses the default
/// path-to-path copy semantics on a <c>with:</c> block, used when a
/// rule is purely a wrapper for nested mappings (<c>followedBy:</c>)
/// or for constant assignments (<c>manual:</c>).
/// </summary>
public enum WithType
{
    /// <summary>No <c>type:</c> declared — default path-to-path copy.</summary>
    Default,

    /// <summary>The <c>type: NONE</c> wrapper-only marker.</summary>
    None,
}

/// <summary>
/// Extension mutation verb declared on a rule in an extension file —
/// determines how the extension's rule merges with the underlying
/// model mapping's matching rule (by <c>name</c>).
/// </summary>
public enum ExtensionAction
{
    /// <summary>The rule appends behaviour to the matching model rule.</summary>
    Add,

    /// <summary>The rule replaces the matching model rule.</summary>
    Overwrite,

    /// <summary>The rule removes the matching model rule.</summary>
    Remove,
}
