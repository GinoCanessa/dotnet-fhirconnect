using System.Linq;
using DotnetFhirConnect.Tests.Mappings;
using DotnetFhirConnect.Validation;
using Xunit;

namespace DotnetFhirConnect.Tests.Validation;

public sealed class ValidatorAcceptsVitalStatusBundleTests
{
    [Fact]
    public void Validate_VitalStatusModelFile_IsValid_NoIssues()
    {
        ValidationReport report = FhirConnectValidator.Validate(FixtureLocator.ModelFile);
        // Surface any issues for easy debugging.
        Assert.True(report.IsValid, BuildSummary(report));
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Validate_VitalStatusProjectDirectory_IsValid_NoIssues()
    {
        ValidationReport report = FhirConnectValidator.Validate(FixtureLocator.ProjectDir);
        Assert.True(report.IsValid, BuildSummary(report));
        Assert.Empty(report.Issues);
    }

    private static string BuildSummary(ValidationReport report)
    {
        return string.Join("\n",
            report.Issues.Select(i => $"[{i.Severity} {i.Code}] {i.FilePath}:{i.Line ?? 0}:{i.Column ?? 0} {i.Pointer} — {i.Message}"));
    }
}
