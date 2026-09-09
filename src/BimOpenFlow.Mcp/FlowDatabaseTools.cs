using Ara3D.MCP;
using BimOpenFlow.Host;

namespace BimOpenFlow.Mcp;

/// <summary>Schema discovery for DuckDB files: what an agent reads before it
/// writes the SQL of a duck.query node. Read-only, and the same probe the host
/// uses for table suggestions.</summary>
public static class FlowDatabaseTools
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
                "Describes every table in a .duckdb file: columns with their DuckDB types, and row "
                + "counts. Read this before writing SQL for a duck.query node.",
                McpSchema.Object()
                    .String("path", "Path to the .duckdb file (see listDatabases).", required: true)
                    .Build(),
                (args, _) => ToolRunner.RunAsync(
                    () => DescribeDatabase(args.GetRequiredString("path")),
                    ["addNode"]));

    public static object ListDatabases(FlowServices s)
        => DuckDbTableProbe.ListDatabases(s.Host.Catalog.Roots)
            .Select(d => new { name = d.Name, path = d.Path, sizeBytes = d.SizeBytes })
            .ToList();

    public static object DescribeDatabase(string path)
        => new
        {
            path,
            tables = DuckDbTableProbe.Describe(path)
                .Select(t => new
                {
                    name = t.Name,
                    rowCount = t.RowCount,
                    columns = t.Columns.Select(c => new { name = c.Name, type = c.Type }).ToList(),
                })
                .ToList(),
        };
}
