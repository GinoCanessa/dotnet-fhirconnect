using System;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.Support;
using Hl7.Fhir.Model;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Inverse of <see cref="OpenEhrToFhirTranslator"/>: converts FHIR
/// shapes back into the openEHR Reference Model values
/// <see cref="OpenEhrPathWriter"/> can write. Conservative coverage —
/// every shape that <see cref="OpenEhrToFhirTranslator"/> emits has a
/// reverse arm here so the two are provably inverse on the value
/// domain. Anything else falls through to a pass-through.
/// </summary>
internal static class FhirToOpenEhrTranslator
{
    /// <summary>
    /// Translate a FHIR value to the form
    /// <see cref="OpenEhrPathWriter"/> can write back into the typed
    /// Composition graph.
    /// </summary>
    public static object? Translate(object? fhirValue)
    {
        switch (fhirValue)
        {
            case null:
                return null;

            case CodeableConcept cc:
                Coding? first = cc.Coding is { Count: > 0 } ? cc.Coding[0] : null;
                if (first is null)
                {
                    return cc.Text is { } txt ? new DvText { Value = txt } : null;
                }
                return new DvCodedText
                {
                    Value = first.Display ?? cc.Text ?? first.Code ?? string.Empty,
                    DefiningCode = new CodePhrase
                    {
                        TerminologyId = new TerminologyId { Value = first.System ?? string.Empty },
                        CodeString = first.Code ?? string.Empty,
                    },
                };

            case Coding c:
                return new DvCodedText
                {
                    Value = c.Display ?? c.Code ?? string.Empty,
                    DefiningCode = new CodePhrase
                    {
                        TerminologyId = new TerminologyId { Value = c.System ?? string.Empty },
                        CodeString = c.Code ?? string.Empty,
                    },
                };

            case FhirDateTime fdt when fdt.Value is { } s && !string.IsNullOrEmpty(s):
                return new DvDateTime { Value = IsoDateTime.Parse(s.AsSpan()) };

            case FhirString fs:
                return new DvText { Value = fs.Value ?? string.Empty };

            case Markdown md:
                return new DvText { Value = md.Value ?? string.Empty };

            case Annotation ann:
                return new DvText { Value = ann.Text ?? string.Empty };

            case ResourceReference rr:
                return RewriteReference(rr);

            case string s:
                return new DvText { Value = s };

            default:
                return fhirValue;
        }
    }

    private static DvEhrUri RewriteReference(ResourceReference rr)
    {
        // Inverse of OpenEhrToFhirTranslator.RewriteEhrUri:
        // "Observation/<id>" → "ehr:///compositions/<id>".
        // Anything else passes through as the raw Reference string.
        string? referenceText = rr.Reference;
        if (!string.IsNullOrEmpty(referenceText) &&
            referenceText.StartsWith("Observation/", StringComparison.Ordinal))
        {
            string id = referenceText.Substring("Observation/".Length);
            return new DvEhrUri { Value = $"ehr:///compositions/{id}" };
        }
        if (!string.IsNullOrEmpty(referenceText))
        {
            return new DvEhrUri { Value = referenceText };
        }
        // Fall back to display when no reference text is set.
        return new DvEhrUri { Value = rr.Display ?? string.Empty };
    }
}
