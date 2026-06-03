using System;
using DotnetFhirConnect.Fhir.R4B;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

public sealed class R4BAdapterPendingTests
{
    [Fact]
    public void ParseResource_Throws_NotImplemented_WithR4BMessage()
    {
        R4BAdapter adapter = new R4BAdapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.ParseResource("{}".AsSpan()));
        Assert.Contains("R4B", ex.Message);
    }

    [Fact]
    public void SerializeResource_Throws_NotImplemented_WithR4BMessage()
    {
        R4BAdapter adapter = new R4BAdapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.SerializeResource(new object()));
        Assert.Contains("R4B", ex.Message);
    }

    [Fact]
    public void CreateResource_Throws_NotImplemented_WithR4BMessage()
    {
        R4BAdapter adapter = new R4BAdapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.CreateResource("Observation"));
        Assert.Contains("R4B", ex.Message);
    }
}
