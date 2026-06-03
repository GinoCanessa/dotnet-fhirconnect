using System;
using DotnetFhirConnect.Fhir.R5;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

public sealed class R5AdapterPendingTests
{
    [Fact]
    public void ParseResource_Throws_NotImplemented_WithR5Message()
    {
        R5Adapter adapter = new R5Adapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.ParseResource("{}".AsSpan()));
        Assert.Contains("R5", ex.Message);
    }

    [Fact]
    public void SerializeResource_Throws_NotImplemented_WithR5Message()
    {
        R5Adapter adapter = new R5Adapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.SerializeResource(new object()));
        Assert.Contains("R5", ex.Message);
    }

    [Fact]
    public void CreateResource_Throws_NotImplemented_WithR5Message()
    {
        R5Adapter adapter = new R5Adapter();
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => adapter.CreateResource("Observation"));
        Assert.Contains("R5", ex.Message);
    }
}
