extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using Annotation = coreR4::Hl7.Fhir.Model.Annotation;
using DotnetFhirConnect.Fhir.R4;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Fhir;

/// <summary>
/// Phase 5 — nested-CodeableConcept setter arms on
/// <see cref="R4Adapter"/>. Pins the merge-in-place semantics
/// (separate writes to <c>code.coding[0].(code|system|display)</c>
/// must accumulate on the same <c>Coding</c> rather than each one
/// blowing away the previous).
/// </summary>
public sealed class R4Adapter_NestedCodeableConceptTests
{
    [Fact]
    public void Code_Coding0_Code_System_Display_MergeInPlace()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation
        {
            Code = new CodeableConcept
            {
                Coding = [new Coding(system: null, code: null, display: "alive")],
            },
        };

        Assert.True(adapter.TrySetValue(obs, "code.coding[0].code", "67162-8", out string? e1), e1);
        Assert.True(adapter.TrySetValue(obs, "code.coding[0].system", "http://loinc.org", out string? e2), e2);
        Assert.True(adapter.TrySetValue(obs, "code.coding[0].display", "Date of death status", out string? e3), e3);

        Coding only = Assert.Single(obs.Code.Coding);
        Assert.Equal("67162-8", only.Code);
        Assert.Equal("http://loinc.org", only.System);
        Assert.Equal("Date of death status", only.Display);
    }

    [Fact]
    public void Category0_Coding0_Code_System_MergeInPlace()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();

        Assert.True(adapter.TrySetValue(obs, "category[0].coding[0].code", "survey", out string? e1), e1);
        Assert.True(adapter.TrySetValue(obs, "category[0].coding[0].system",
            "http://terminology.hl7.org/CodeSystem/observation-category", out string? e2), e2);

        CodeableConcept cc = Assert.Single(obs.Category);
        Coding only = Assert.Single(cc.Coding);
        Assert.Equal("survey", only.Code);
        Assert.Equal("http://terminology.hl7.org/CodeSystem/observation-category", only.System);
    }

    [Fact]
    public void NestedCodingFallback_Path_WalksHigherIndices()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();

        Assert.True(adapter.TrySetValue(obs, "category[0].coding[1].code", "ALPHA", out string? e1), e1);
        Assert.True(adapter.TrySetValue(obs, "category[0].coding[1].system", "urn:test", out string? e2), e2);

        CodeableConcept cc = Assert.Single(obs.Category);
        Assert.Equal(2, cc.Coding.Count);
        Assert.Equal("ALPHA", cc.Coding[1].Code);
        Assert.Equal("urn:test", cc.Coding[1].System);
    }

    [Fact]
    public void NestedCoding_ReadBack_Symmetric()
    {
        R4Adapter adapter = new R4Adapter();
        Observation obs = new Observation();

        Assert.True(adapter.TrySetValue(obs, "code.coding[0].code", "67162-8", out string? e1), e1);
        Assert.True(adapter.TrySetValue(obs, "code.coding[0].system", "http://loinc.org", out string? e2), e2);

        Assert.True(adapter.TryGetValue(obs, "code.coding[0].code", out object? readCode));
        Assert.True(adapter.TryGetValue(obs, "code.coding[0].system", out object? readSystem));
        Assert.Equal("67162-8", readCode);
        Assert.Equal("http://loinc.org", readSystem);
    }
}
