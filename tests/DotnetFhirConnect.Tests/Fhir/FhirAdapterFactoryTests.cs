using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using DotnetFhirConnect.Fhir.R4B;
using DotnetFhirConnect.Fhir.R5;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

public sealed class FhirAdapterFactoryTests
{
    [Fact]
    public void Create_R4_ReturnsR4Adapter()
    {
        IFhirAdapter adapter = FhirAdapterFactory.Create(FhirRelease.R4);
        Assert.IsType<R4Adapter>(adapter);
        Assert.Equal(FhirRelease.R4, adapter.Release);
    }

    [Fact]
    public void Create_R4B_ReturnsR4BAdapter()
    {
        IFhirAdapter adapter = FhirAdapterFactory.Create(FhirRelease.R4B);
        Assert.IsType<R4BAdapter>(adapter);
        Assert.Equal(FhirRelease.R4B, adapter.Release);
    }

    [Fact]
    public void Create_R5_ReturnsR5Adapter()
    {
        IFhirAdapter adapter = FhirAdapterFactory.Create(FhirRelease.R5);
        Assert.IsType<R5Adapter>(adapter);
        Assert.Equal(FhirRelease.R5, adapter.Release);
    }
}
