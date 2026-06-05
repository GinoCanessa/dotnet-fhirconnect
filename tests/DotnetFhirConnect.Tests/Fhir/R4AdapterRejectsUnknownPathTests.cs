extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using DotnetFhirConnect.Fhir.R4;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

public sealed class R4AdapterRejectsUnknownPathTests
{
    [Fact]
    public void TrySetValue_OnBogusPath_ReturnsFalseWithError()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");

        bool set = adapter.TrySetValue(obs, "Observation.bogus.field", "anything", out string? error);

        Assert.False(set);
        Assert.NotNull(error);
        Assert.Contains("bogus", error);
    }

    [Fact]
    public void TryGetValue_OnBogusPath_ReturnsFalseWithNullValue()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");

        bool got = adapter.TryGetValue(obs, "Observation.bogus.field", out object? value);

        Assert.False(got);
        Assert.Null(value);
    }
}
