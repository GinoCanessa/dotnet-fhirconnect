using System;
using System.IO;
using System.Linq;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

public sealed class ValidatorRejectsContextStartMismatchTests
{
    [Fact]
    public void Validate_ContextStartNotInArchetypes_ProducesFcv010Error()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-startmiss-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(tempFile, """
            grammar: FHIRConnect/v1.0.0
            type: context
            metadata:
              name: BadContext
              version: 0
            spec:
              system: FHIR
              version: R4
            context:
              profile:
                url: http://example.org/profile
              template:
                id: T
              archetypes:
                - EVALUATION.something.v1
              extensions: []
              start: EVALUATION.different.v1
            """);

        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.False(report.IsValid);
            ValidationIssue issue = Assert.Single(report.Issues,
                i => i.Code == "FCV010");
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal("/context/start", issue.Pointer);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
