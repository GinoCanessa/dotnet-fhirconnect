using System;
using System.IO;
using DotnetFhirConnect.Fhir;
using DotnetFhirConnect.Fhir.R4;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 5 R4 adapter tests — round-trip parse/serialize on the
/// vendored Observation fixture plus path-set/get on note.text.
/// </summary>
public sealed class R4AdapterTests
{
    private static string ObservationFixture => Path.Combine(
        AppContext.BaseDirectory, "fixtures", "vital-status", "samples", "vital-status.observation.r4.json");

    [Fact]
    public void Parse_ThenSerialize_RoundTripsSemanticEquivalence()
    {
        R4Adapter adapter = new R4Adapter();
        string json = File.ReadAllText(ObservationFixture);

        object parsed = adapter.ParseResource(json.AsSpan());
        Observation obs = Assert.IsType<Observation>(parsed);

        string roundTrippedJson = adapter.SerializeResource(obs);
        object reparsed = adapter.ParseResource(roundTrippedJson.AsSpan());

        Observation obs2 = Assert.IsType<Observation>(reparsed);
        Assert.True(obs.IsExactly(obs2),
            "Observation must be semantically equivalent after a round-trip serialize+parse.");
    }

    [Fact]
    public void TrySetValue_ThenTryGetValue_OnNoteText_RoundTrips()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = (Observation)adapter.CreateResource("Observation");

        bool set = adapter.TrySetValue(obs, "Observation.note.text", "Hello from a unit test.", out string? error);
        Assert.True(set, error);
        Assert.Null(error);

        bool got = adapter.TryGetValue(obs, "Observation.note.text", out object? value);
        Assert.True(got);
        Assert.Equal("Hello from a unit test.", value);
    }

    [Fact]
    public void CreateResource_Observation_ReturnsTypedInstance()
    {
        R4Adapter adapter = new R4Adapter();
        object o = adapter.CreateResource("Observation");
        Assert.IsType<Observation>(o);
    }

    [Fact]
    public void Release_ReturnsR4()
    {
        R4Adapter adapter = new R4Adapter();
        Assert.Equal(FhirRelease.R4, adapter.Release);
    }
}
