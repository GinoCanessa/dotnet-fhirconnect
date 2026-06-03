using System;
using System.IO;
using System.Linq;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

/// <summary>
/// Pins the validator's "every issue has a JSON pointer; line/column
/// are best-effort but at least one issue carries non-null Line"
/// promise. Locators are how users find their typo in a 200-line YAML
/// file.
/// </summary>
public sealed class ValidatorReportsLineNumberTests
{
    [Fact]
    public void Validate_BadContextStart_AttachesPointerAndLineNumber()
    {
        string tempFile = Path.Combine(
            Path.GetTempPath(), $"fc-locator-{Guid.NewGuid():N}.yaml");
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
              start: EVALUATION.elsewhere.v1
            """);

        try
        {
            ValidationReport report = FhirConnectValidator.Validate(tempFile);
            Assert.False(report.IsValid);

            // Every issue MUST have a non-empty JSON pointer.
            Assert.All(report.Issues, i => Assert.False(string.IsNullOrEmpty(i.Pointer)));

            // At least one issue carries a non-null Line so users can locate the failure.
            Assert.Contains(report.Issues, i => i.Line is not null);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
