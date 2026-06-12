extern alias coreR4;
using Observation = coreR4::Hl7.Fhir.Model.Observation;
using System;
using DotnetFhirConnect.Engine;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.Support;
using Hl7.Fhir.Model;
using Xunit;

namespace DotnetFhirConnect.Tests.Engine;

/// <summary>
/// Phase 1 — round-trip every arm of
/// <see cref="OpenEhrToFhirTranslator"/> through
/// <see cref="FhirToOpenEhrTranslator"/> so the two are provably
/// inverse on the value domain.
/// </summary>
public sealed class FhirToOpenEhrTranslatorTests
{
    [Fact]
    public void DvCodedText_RoundTripsThroughCodeableConcept()
    {
        DvCodedText src = new DvCodedText
        {
            Value = "Date of death status",
            DefiningCode = new CodePhrase
            {
                TerminologyId = new TerminologyId { Value = "http://loinc.org" },
                CodeString = "67162-8",
            },
        };

        object? toFhir = OpenEhrToFhirTranslator.Translate(src);
        CodeableConcept cc = Assert.IsType<CodeableConcept>(toFhir);

        object? back = FhirToOpenEhrTranslator.Translate(cc);
        DvCodedText reverted = Assert.IsType<DvCodedText>(back);
        Assert.Equal(src.Value, reverted.Value);
        Assert.Equal(src.DefiningCode.CodeString, reverted.DefiningCode.CodeString);
        Assert.Equal(src.DefiningCode.TerminologyId.Value, reverted.DefiningCode.TerminologyId.Value);
    }

    [Fact]
    public void DvText_RoundTripsThroughString()
    {
        DvText src = new DvText { Value = "free text" };

        object? toFhir = OpenEhrToFhirTranslator.Translate(src);
        string s = Assert.IsType<string>(toFhir);
        Assert.Equal("free text", s);

        object? back = FhirToOpenEhrTranslator.Translate(s);
        DvText reverted = Assert.IsType<DvText>(back);
        Assert.Equal(src.Value, reverted.Value);
    }

    [Fact]
    public void DvDateTime_RoundTripsThroughFhirDateTime()
    {
        DvDateTime src = new DvDateTime
        {
            Value = IsoDateTime.Parse("2023-06-15T12:34:56Z".AsSpan()),
        };

        object? toFhir = OpenEhrToFhirTranslator.Translate(src);
        FhirDateTime fdt = Assert.IsType<FhirDateTime>(toFhir);
        Assert.Equal("2023-06-15T12:34:56Z", fdt.Value);

        object? back = FhirToOpenEhrTranslator.Translate(fdt);
        DvDateTime reverted = Assert.IsType<DvDateTime>(back);
        DateTimeOffset originalDto = DateTimeOffset.Parse(fdt.Value!);
        DateTimeOffset revertedDto = DateTimeOffset.Parse(reverted.Value.ToString());
        Assert.Equal(originalDto, revertedDto);
    }

    [Fact]
    public void DvEhrUri_CompositionPath_RoundTripsThroughObservationReference()
    {
        DvEhrUri src = new DvEhrUri { Value = "ehr:///compositions/abc-123" };

        object? toFhir = OpenEhrToFhirTranslator.Translate(src);
        ResourceReference rr = Assert.IsType<ResourceReference>(toFhir);
        Assert.Equal("Observation/abc-123", rr.Reference);

        object? back = FhirToOpenEhrTranslator.Translate(rr);
        DvEhrUri reverted = Assert.IsType<DvEhrUri>(back);
        Assert.Equal(src.Value, reverted.Value);
    }

    [Fact]
    public void DvEhrUri_OtherScheme_PassesThroughAsRawReference()
    {
        DvEhrUri src = new DvEhrUri { Value = "urn:uuid:11112222-3333-4444" };

        object? toFhir = OpenEhrToFhirTranslator.Translate(src);
        ResourceReference rr = Assert.IsType<ResourceReference>(toFhir);
        Assert.Equal("urn:uuid:11112222-3333-4444", rr.Reference);

        object? back = FhirToOpenEhrTranslator.Translate(rr);
        DvEhrUri reverted = Assert.IsType<DvEhrUri>(back);
        Assert.Equal(src.Value, reverted.Value);
    }

    [Fact]
    public void Null_ReturnsNull()
    {
        Assert.Null(FhirToOpenEhrTranslator.Translate(null));
    }
}
