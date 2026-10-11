using System.Text;
using Ara3D.BimOpenSchema;

namespace BimOpenFlow.NodeDocs;

/// <summary>Writes the column vocabulary (the ColumnKinds registry in bim-open-data) as
/// columns.json, for the editor and the guides to read without a build, and columns.md for
/// people.</summary>
public static class ColumnDocs
{
    public const string JsonFileName = "columns.json";
    public const string MarkdownFileName = "columns.md";

    private static readonly IReadOnlyList<(ColumnRole Role, string Heading, string Use)> Sections =
    [
        (ColumnRole.Key, "Keys", "Identify a row or an entity."),
        (ColumnRole.Label, "Labels", "Name a row for a person."),
        (ColumnRole.Category, "Categories", "Group rows into a small set of values."),
        (ColumnRole.Measure, "Measures", "Quantities to sum, average or chart."),
        (ColumnRole.Reference, "References", "Point at a row of another table."),
    ];

    public static string Json() => ColumnKinds.ToJson();

    public static string Markdown()
    {
        var text = new StringBuilder();
        text.AppendLine("# Column vocabulary");
        text.AppendLine();
        text.AppendLine("> GENERATED FILE — do not edit by hand. Written by `bimopenflow-studio nodedocs` from the");
        text.AppendLine("> `ColumnKinds` registry in `Ara3D.BimOpenSchema.ObjectModel` (bim-open-data). To change it,");
        text.AppendLine("> change the registry and regenerate. `columns.json` holds the same list for programs.");
        text.AppendLine();
        text.AppendLine("A column kind is metadata on a column of a table: a name, a scalar type, an optional unit, a role and a meaning.");
        text.AppendLine("It is not a wire type; tables stay the currency. Names match ignoring case, as DuckDB identifiers do.");
        foreach (var (role, heading, use) in Sections)
        {
            var kinds = ColumnKinds.All.Where(k => k.Role == role).ToList();
            if (kinds.Count == 0)
                continue;
            text.AppendLine();
            text.AppendLine($"## {heading}");
            text.AppendLine();
            text.AppendLine(use);
            text.AppendLine();
            text.AppendLine("| Name | Type | Unit | Role | Meaning |");
            text.AppendLine("|---|---|---|---|---|");
            foreach (var kind in kinds)
                text.AppendLine($"| `{kind.Name}` | {kind.Type} | {kind.Unit ?? ""} | {kind.Role} | {Cell(kind.Meaning)} |");
        }
        return text.ToString();
    }

    /// <summary>Writes columns.json and columns.md into <paramref name="dir"/>; returns the two paths.</summary>
    public static IReadOnlyList<string> Write(string dir)
    {
        var json = Path.Combine(dir, JsonFileName);
        var markdown = Path.Combine(dir, MarkdownFileName);
        File.WriteAllText(json, Json());
        File.WriteAllText(markdown, Markdown());
        return [json, markdown];
    }

    private static string Cell(string text) => text.Replace("|", @"\|");
}
