using System;
using System.Collections.Generic;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Builds a minimal <see cref="Composition"/> skeleton that the
/// inverse mapping walk (<see cref="OpenEhrPathWriter"/>) can target.
/// v0.x ships a single archetype shape — vital_status — per the
/// scope envelope; any other archetype id throws
/// <see cref="NotSupportedException"/>.
/// </summary>
internal static class SkeletonBuilder
{
    private const string VitalStatusEvaluationArchetype = "openEHR-EHR-EVALUATION.vital_status.v1";
    private const string CompositionReportArchetype = "openEHR-EHR-COMPOSITION.report.v1";
    private const string RmVersion = "1.0.4";

    /// <summary>
    /// Construct an empty Composition wrapping an empty
    /// <see cref="Evaluation"/> for the requested archetype.
    /// </summary>
    public static Composition ForArchetype(string archetypeId)
    {
        ArgumentNullException.ThrowIfNull(archetypeId);
        if (!string.Equals(archetypeId, VitalStatusEvaluationArchetype, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                "FhirConnectEngine.ToOpenEhr: skeleton bootstrap is vital_status-only in v0.x; " +
                "pass a pre-built skeleton via the internal overload for other archetypes.");
        }

        Evaluation eval = new Evaluation
        {
            Name = new DvText { Value = "Vital status" },
            ArchetypeNodeId = VitalStatusEvaluationArchetype,
            ArchetypeDetails = new Archetyped
            {
                ArchetypeId = new ArchetypeId { Value = VitalStatusEvaluationArchetype },
                RmVersion = RmVersion,
            },
            Language = IsoLanguage("en"),
            Encoding = Encoding("UTF-8"),
            Subject = new PartySelf(),
            Data = new ItemTree
            {
                Name = new DvText { Value = "Tree" },
                ArchetypeNodeId = "at0001",
                Items = new List<Item>(),
            },
            Protocol = new ItemTree
            {
                Name = new DvText { Value = "Tree" },
                ArchetypeNodeId = "at0002",
                Items = new List<Item>(),
            },
        };

        Composition composition = new Composition
        {
            Name = new DvText { Value = "Vitalstatus" },
            ArchetypeNodeId = CompositionReportArchetype,
            ArchetypeDetails = new Archetyped
            {
                ArchetypeId = new ArchetypeId { Value = CompositionReportArchetype },
                RmVersion = RmVersion,
            },
            Language = IsoLanguage("en"),
            Territory = IsoTerritory("US"),
            Category = new DvCodedText
            {
                Value = "event",
                DefiningCode = new CodePhrase
                {
                    TerminologyId = new TerminologyId { Value = "openehr" },
                    CodeString = "433",
                },
            },
            Composer = new PartyIdentified(),
            Context = new EventContext
            {
                StartTime = new DvDateTime
                {
                    Value = IsoDateTime.Parse("1970-01-01T00:00:00Z".AsSpan()),
                },
                Setting = new DvCodedText
                {
                    Value = "other care",
                    DefiningCode = new CodePhrase
                    {
                        TerminologyId = new TerminologyId { Value = "openehr" },
                        CodeString = "238",
                    },
                },
            },
            Content = new List<ContentItem> { eval },
        };

        return composition;
    }

    private static CodePhrase IsoLanguage(string code) => new CodePhrase
    {
        TerminologyId = new TerminologyId { Value = "ISO_639-1" },
        CodeString = code,
    };

    private static CodePhrase IsoTerritory(string code) => new CodePhrase
    {
        TerminologyId = new TerminologyId { Value = "ISO_3166-1" },
        CodeString = code,
    };

    private static CodePhrase Encoding(string code) => new CodePhrase
    {
        TerminologyId = new TerminologyId { Value = "IANA_character-sets" },
        CodeString = code,
    };
}
