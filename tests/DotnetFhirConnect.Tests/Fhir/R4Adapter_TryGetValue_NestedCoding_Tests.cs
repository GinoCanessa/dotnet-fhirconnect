using DotnetFhirConnect.Fhir.R4;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 1 — round-trip the new <c>code.coding[0].(code|system)</c>
/// and <c>category[0].coding[0].(code|system)</c> getter arms added
/// to <see cref="R4Adapter.TryGetValue"/> for the Phase 6b
/// extension-injected-value readback path.
/// </summary>
public sealed class R4Adapter_TryGetValue_NestedCoding_Tests
{
    [Fact]
    public void Code_Coding0_Code_ReadsBack()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation
        {
            Code = new CodeableConcept { Coding = [new Coding("http://loinc.org", "67162-8", "Date of death status")] },
        };

        Assert.True(adapter.TryGetValue(obs, "code.coding[0].code", out object? value));
        Assert.Equal("67162-8", value);
    }

    [Fact]
    public void Code_Coding0_System_ReadsBack()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation
        {
            Code = new CodeableConcept { Coding = [new Coding("http://loinc.org", "67162-8")] },
        };

        Assert.True(adapter.TryGetValue(obs, "code.coding[0].system", out object? value));
        Assert.Equal("http://loinc.org", value);
    }

    [Fact]
    public void Category0_Coding0_Code_ReadsBack()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();
        obs.Category.Add(new CodeableConcept
        {
            Coding = [new Coding("http://terminology.hl7.org/CodeSystem/observation-category", "survey")],
        });

        Assert.True(adapter.TryGetValue(obs, "category[0].coding[0].code", out object? value));
        Assert.Equal("survey", value);
    }

    [Fact]
    public void Category0_Coding0_System_ReadsBack()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();
        obs.Category.Add(new CodeableConcept
        {
            Coding = [new Coding("http://terminology.hl7.org/CodeSystem/observation-category", "survey")],
        });

        Assert.True(adapter.TryGetValue(obs, "category[0].coding[0].system", out object? value));
        Assert.Equal("http://terminology.hl7.org/CodeSystem/observation-category", value);
    }

    [Fact]
    public void NestedCoding_OnEmptyCode_ReturnsFalse()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();

        Assert.False(adapter.TryGetValue(obs, "code.coding[0].code", out object? v1));
        Assert.Null(v1);
        Assert.False(adapter.TryGetValue(obs, "category[0].coding[0].system", out object? v2));
        Assert.Null(v2);
    }
}
