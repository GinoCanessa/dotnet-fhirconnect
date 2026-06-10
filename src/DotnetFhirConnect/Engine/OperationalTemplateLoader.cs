using System.Diagnostics.CodeAnalysis;
using System.IO;
using DotnetOpenEhr.Templates;

namespace DotnetFhirConnect.Engine;

/// <summary>
/// Loads an openEHR OPT1.4 XML operational template via the SDK's
/// <see cref="Opt14XmlParser"/> and wraps it in an
/// <see cref="OptTemplateAdapter"/>. Strict parse by default; the SDK's
/// <see cref="Opt14ParseException"/> propagates unswallowed.
/// </summary>
internal static class OperationalTemplateLoader
{
    [RequiresUnreferencedCode(
        "Loads OPT1.4 XML via DotnetOpenEhr.Templates (LINQ-to-XML). Not AOT-publishable in v0.x.")]
    public static IOperationalTemplate Load(Stream optXml)
    {
        return new OptTemplateAdapter(Opt14XmlParser.Load(optXml));
    }

    [RequiresUnreferencedCode(
        "Loads OPT1.4 XML via DotnetOpenEhr.Templates (LINQ-to-XML). Not AOT-publishable in v0.x.")]
    public static IOperationalTemplate Load(string optFilePath)
    {
        return new OptTemplateAdapter(Opt14XmlParser.Load(optFilePath));
    }
}
