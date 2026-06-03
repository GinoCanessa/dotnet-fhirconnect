using System;
using System.IO;
using System.Linq;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

/// <summary>
/// Two-shot test: confirms (a) the semantic archetype-id check
/// catches a malformed openEHR id, and (b) the schema-level pointer
/// path is wired up correctly when a model file is missing the
/// required <c>spec.version</c> field.
/// </summary>
public sealed class ValidatorRejectsMissingArchetypeTests
{
    [Fact]
    public void Validate_ModelWithMalformedArchetypeId_RaisesFcv005OnArchetypePointer()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-badarch-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, """
            grammar: FHIRConnect/v1.0.0
            type: model
            metadata:
              name: EVALUATION.missing.v1
              version: "0"
            spec:
              system: FHIR
              version: R4
              openEhrConfig:
                archetype: not-an-archetype-id
              fhirConfig:
                structureDefinition: http://hl7.org/fhir/StructureDefinition/Observation
            mappings: []
            """);

        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.False(report.IsValid);
            ValidationIssue issue = Assert.Single(report.Issues, i => i.Code == "FCV005");
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal("/spec/openEhrConfig/archetype", issue.Pointer);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_ModelMissingSpecVersion_RaisesSchemaErrorOnSpecPointer()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-nover-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, """
            grammar: FHIRConnect/v1.0.0
            type: model
            metadata:
              name: EVALUATION.missing.v1
              version: "0"
            spec:
              system: FHIR
            mappings: []
            """);

        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.False(report.IsValid);
            Assert.Contains(report.Issues,
                i => i.Severity == ValidationSeverity.Error &&
                     i.Pointer.StartsWith("/spec", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
