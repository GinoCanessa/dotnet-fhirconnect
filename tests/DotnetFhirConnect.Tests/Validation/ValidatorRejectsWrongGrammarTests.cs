using System;
using System.IO;
using System.Linq;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

public sealed class ValidatorRejectsWrongGrammarTests
{
    [Fact]
    public void Validate_GrammarV090_ProducesSingleErrorWithCodeFcv001()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-grammar-{Guid.NewGuid():N}.yml");
        File.WriteAllText(tempFile, """
            grammar: FHIRConnect/v0.9.0
            type: model
            metadata:
              name: dummy
              version: 0
            spec:
              system: FHIR
              version: R4
            mappings: []
            """);

        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.False(report.IsValid);
            ValidationIssue issue = Assert.Single(report.Issues);
            Assert.Equal(ValidationSeverity.Error, issue.Severity);
            Assert.Equal("FCV001", issue.Code);
            Assert.Contains("FHIRConnect/v0.9.0", issue.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
