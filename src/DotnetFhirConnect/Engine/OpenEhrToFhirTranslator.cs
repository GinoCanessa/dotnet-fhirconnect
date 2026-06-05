using System;
using System.Linq;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Basic;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.DataTypes.Uri;
using Hl7.Fhir.Model;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Converts openEHR Reference Model values into shapes the
/// <see cref="DotnetFhirConnect.Fhir.IFhirAdapter"/> can write.
/// Conservative coverage — every Dv* type the vital_status walking
/// skeleton actually touches. Anything else falls through to a
/// best-effort <see cref="object.ToString"/> string.
/// </summary>
internal static class OpenEhrToFhirTranslator
{
    /// <summary>
    /// Translate an openEHR value to the form the
    /// <see cref="DotnetFhirConnect.Fhir.R4.R4Adapter"/>'s
    /// <c>TrySetValue</c> expects. The adapter does its own coercion
    /// past the boundary; this method just narrows the option space.
    /// </summary>
    public static object? Translate(object? openEhrValue)
    {
        switch (openEhrValue)
        {
            case null:
                return null;

            case DvCodedText coded:
                return new CodeableConcept
                {
                    Coding =
                    [
                        new Coding
                        {
                            System = coded.DefiningCode?.TerminologyId?.Value,
                            Code = coded.DefiningCode?.CodeString,
                            Display = coded.Value,
                        }
                    ],
                };

            case DvText text:
                return text.Value;

            case DvDateTime dt:
                // DvDateTime.Value is an IsoDateTime; FhirDateTime
                // accepts a string in the canonical ISO-8601 form.
                return new FhirDateTime(dt.Value.ToString());

            case DvEhrUri uri:
                // Best-effort: rewrite `ehr:///compositions/<uuid>` →
                // `Observation/<uuid>` for the link → ResourceReference
                // path. Anything else is passed through as-is for the
                // adapter to wrap.
                return RewriteEhrUri(uri.Value);

            case PartyIdentified party:
                return new ResourceReference { Display = party.Name };

            case PartyProxy proxy:
                return new ResourceReference { Display = proxy?.ToString() };

            case Participation participation:
                // Engine treats participations by iterating and
                // recursing via followedBy; if a translator caller
                // ends up with one directly, surface the performer.
                return participation.Performer is PartyIdentified perfP
                    ? new ResourceReference { Display = perfP.Name }
                    : null;

            case DvIdentifier ident:
                return new Identifier
                {
                    Value = ident.Id,
                    System = ident.Issuer,
                };

            case Cluster cluster:
                return ExtractClusterIdentifier(cluster);

            default:
                return openEhrValue;
        }
    }

    private static Identifier ExtractClusterIdentifier(Cluster cluster)
    {
        // For v0.x reference-rule recursion: pull the first
        // DvIdentifier (or DvText) value out of the cluster's items
        // so downstream adapter writes get a meaningful Identifier.
        if (cluster.Items is null)
        {
            return new Identifier();
        }
        foreach (Item item in cluster.Items)
        {
            if (item is DotnetOpenEhr.Rm.DataStructures.Element { Value: DvIdentifier ident })
            {
                return new Identifier { Value = ident.Id, System = ident.Issuer };
            }
            if (item is DotnetOpenEhr.Rm.DataStructures.Element { Value: DvText dt })
            {
                return new Identifier { Value = dt.Value };
            }
        }
        return new Identifier();
    }

    private static ResourceReference RewriteEhrUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return new ResourceReference();
        }
        const string prefix = "ehr:///compositions/";
        if (uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            string id = uri.Substring(prefix.Length);
            return new ResourceReference { Reference = $"Observation/{id}" };
        }
        return new ResourceReference { Reference = uri };
    }
}
