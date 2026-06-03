using System;
using System.IO;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

/// <summary>
/// Proves that the Phase 2 schema patch (widening
/// <c>spec.version</c> from <c>["R4"]</c> to
/// <c>["R4","R4B","R5"]</c>) actually works end-to-end through the
/// embedded-resource path.
/// </summary>
public sealed class ValidatorAcceptsR4BAndR5SpecVersionTests
{
    [Theory]
    [InlineData("R4")]
    [InlineData("R4B")]
    [InlineData("R5")]
    public void Validate_MinimalModelFileWithRelease_IsValid(string release)
    {
        string tempFile = WriteTempModel(release);
        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.True(report.IsValid,
                $"Expected {release} to validate, got: " +
                string.Join("; ", report.Issues.Select(i => $"{i.Code} {i.Pointer} {i.Message}")));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string WriteTempModel(string release)
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-v-{release}-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, $"""
            grammar: FHIRConnect/v1.0.0
            type: model
            metadata:
              name: EVALUATION.vital_status.v1
              version: 0.0.1-alpha
            spec:
              system: FHIR
              version: {release}
              openEhrConfig:
                archetype: openEHR-EHR-EVALUATION.vital_status.v1
              fhirConfig:
                structureDefinition: http://hl7.org/fhir/StructureDefinition/Observation
            mappings: []
            """);
        return tempFile;
    }
}
