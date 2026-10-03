namespace BimOpenFlow.TestSupport;

/// <summary>Hand-written STEP (IFC) fixtures small enough to read in a test. Line endings are
/// CRLF throughout, as IFC exporters write them.</summary>
public static class MiniIfc
{
    public const string Ifc4 = "IFC4";
    public const string Ifc2x3 = "IFC2X3";
    public const string LineEnding = "\r\n";

    /// <summary>The ISO-10303-21 header up to and including DATA;.</summary>
    public static string Header(string schema = Ifc4, string fileName = "mini.ifc", string description = "")
        => Lines(
            "ISO-10303-21;",
            "HEADER;",
            $"FILE_DESCRIPTION(('{description}'),'2;1');",
            $"FILE_NAME('{fileName}','2026-01-01T00:00:00',(''),(''),'','','');",
            $"FILE_SCHEMA(('{schema}'));",
            "ENDSEC;",
            "DATA;");

    public static readonly string Footer = Lines("ENDSEC;", "END-ISO-10303-21;");

    /// <summary>A complete file around the given entity lines (one #n=... per line).</summary>
    public static string Document(string entities, string schema = Ifc4, string fileName = "mini.ifc", string description = "")
        => Header(schema, fileName, description) + entities.ReplaceLineEndings(LineEnding).Trim() + LineEnding + Footer;

    /// <summary>One IFC4 wall with the owner-history chain it requires and nothing else:
    /// #1 IFCPERSON, #2 IFCORGANIZATION, #3 IFCPERSONANDORGANIZATION, #4 IFCAPPLICATION,
    /// #5 IFCOWNERHISTORY, #6 IFCWALL (name 'W', GlobalId all zeros). Fourteen lines in all.</summary>
    public static readonly string Wall = Document("""
        #1=IFCPERSON($,$,'p',$,$,$,$,$);
        #2=IFCORGANIZATION($,'o',$,$,$);
        #3=IFCPERSONANDORGANIZATION(#1,#2,$);
        #4=IFCAPPLICATION(#2,'1','app','app');
        #5=IFCOWNERHISTORY(#3,#4,$,.ADDED.,$,$,$,0);
        #6=IFCWALL('0000000000000000000000',#5,'W',$,$,$,$,$,$);
        """);

    /// <summary>The entity id of the wall in <see cref="Wall"/>.</summary>
    public const int WallId = 6;

    private static string Lines(params string[] lines)
        => string.Concat(lines.Select(l => l + LineEnding));
}
