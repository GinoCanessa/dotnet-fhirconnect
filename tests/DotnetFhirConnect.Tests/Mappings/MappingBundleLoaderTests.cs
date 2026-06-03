using DotnetFhirConnect.Mappings;
using Xunit;

namespace DotnetFhirConnect.Tests.Mappings;

/// <summary>
/// Loader test for <see cref="MappingBundle.Load(string)"/> —
/// confirms that pointing the loader at the project directory pulls
/// in the context file plus both extensions, keyed by name.
/// </summary>
public sealed class MappingBundleLoaderTests
{
    [Fact]
    public void Load_ProjectDirectory_ReturnsBundleWithContextAndBothExtensions()
    {
        object loaded = FhirConnectMapping.Load(FixtureLocator.ProjectDir);

        MappingBundle bundle = Assert.IsType<MappingBundle>(loaded);
        Assert.NotNull(bundle.Context);
        Assert.Equal("KDS_Vitalstatus.context", bundle.Context!.Metadata.Name.Trim());

        Assert.Empty(bundle.Models);

        Assert.Equal(2, bundle.Extensions.Count);
        Assert.Contains("KDS_composition", bundle.Extensions.Keys);
        Assert.Contains("KDS_vital_status", bundle.Extensions.Keys);
    }
}
