using System;
using System.IO;
using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Loader rejects any grammar declaration other than
/// <c>FHIRConnect/v1.0.0</c>. Exception message must carry both the
/// offending grammar string and the file path so users can find the
/// problem in a multi-file bundle.
/// </summary>
public sealed class LoaderRejectsUnsupportedGrammarTests
{
    [Fact]
    public void Load_UnsupportedGrammar_ThrowsFhirConnectFormatExceptionWithGrammarAndPath()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"fc-loader-test-{Guid.NewGuid():N}.yml");
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
            FhirConnectFormatException ex = Assert.Throws<FhirConnectFormatException>(
                () => FhirConnectMapping.Load(tempFile));

            Assert.Equal(tempFile, ex.FilePath);
            Assert.Equal("FHIRConnect/v0.9.0", ex.GrammarString);
            Assert.Contains("FHIRConnect/v0.9.0", ex.Message);
            Assert.Contains(tempFile, ex.Message);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
