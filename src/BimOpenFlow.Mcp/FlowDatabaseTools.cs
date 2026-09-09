using System.Text.RegularExpressions;
using Ara3D.MCP;
using BimOpenFlow.Host;

namespace BimOpenFlow.Mcp;

/// <summary>Schema discovery for DuckDB files: what an agent reads before it
/// writes the SQL of a duck.query node. Read-only, and the same probe the host
/// uses for table suggestions.</summary>
public static partial class FlowDatabaseTools
{
    public static McpServer RegisterDatabaseTools(this McpServer mcp, FlowServices s)
        => mcp
            .Tool(
                "listDatabases",
                "Lists the .duckdb files under the host's model roots, with the paths to give a "
                + "duck.source node.",
                (_, _) => ToolRunner.RunAsync(() => ListDatabases(s), ["describeDatabase"]))
            .Tool(
                "describeDatabase",
                "Describes a .duckdb file. Without 'table': every table with its row count, and for "
                + "tables that have rows the column names (companion columns ending in _assurance, "
                + "_reason, _explanation, _evidence or _completeness are folded into 'companions'). "
                + "With 'table': that table's full column list with DuckDB types. Read this before "
                + "writing SQL for a duck.query node.",
                McpSchema.Object()
                    .String("path", "Path to the .duckdb file (see listDatabases).", required: true)
                    .String("table", "One table name, for its full column list with types.")
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => DescribeDatabase(args.GetRequiredString("path"), args.GetString("table")),
                    ["addNode", "editGraph"]));

    public static readonly string[] CompanionSuffixes = ["_assurance", "_reason", "_explanation", "_evidence", "_completeness"];

    public static object ListDatabases(FlowServices s)
        => DuckDbTableProbe.ListDatabases(s.Host.Catalog.Roots)
            .Select(d => new { name = d.Name, path = d.Path, sizeBytes = d.SizeBytes })
            .ToList();

    public static object DescribeDatabase(string path, string? table = null)
    {
        var tables = DuckDbTableProbe.Describe(path, table);
        return string.IsNullOrWhiteSpace(table)
            ? new
            {
                path,
                companionNote = "A column x may have companions x_assurance, x_reason, x_explanation, x_evidence "
                    + "(and x_completeness for lists) that say how x was established or why it is NULL; "
                    + "'companions' lists the bases that have them.",
                tables = tables.Select(t => Summarize(t)).ToList(),
            }
            : new
            {
                path,
                tables = tables
                    .Select(t => new
                    {
                        name = t.Name,
                        rowCount = t.RowCount,
                        columns = t.Columns.Select(c => new { name = c.Name, type = c.Type }).ToList(),
                    })
                    .ToList(),
            };
    }

    /// <summary>Row count, base column names, and the bases that carry companion
    /// columns; empty tables get only their name and count, because there is
    /// nothing to query and the column list would just cost tokens.</summary>
    private static object Summarize(DuckTable t)
    {
        if (t.RowCount == 0)
            return new { name = t.Name, rowCount = 0L, columnCount = t.Columns.Count };
        var names = t.Columns.Select(c => c.Name).ToList();
        var bases = names.Where(n => !Companion().IsMatch(n)).ToList();
        var withCompanions = names
            .Select(n => Companion().Match(n))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .Where(bases.Contains)
            .ToList();
        return new { name = t.Name, rowCount = t.RowCount, columns = bases, companions = withCompanions };
    }

    [GeneratedRegex("^(.+)_(assurance|reason|explanation|evidence|completeness)$")]
    private static partial Regex Companion();
}
