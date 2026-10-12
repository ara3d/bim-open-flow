using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Schema rules shared by the DuckDB pack. The nodes turn DuckDB date, time and
/// timestamp columns into ISO text (NormalizeDatesToText), so a schema read from DuckDB is
/// changed the same way before a node reports it.</summary>
internal static class DuckSchemas
{
    /// <summary>The schema as the table on the wire will carry it: dates and timestamps are Text.</summary>
    public static SchemaResult OnTheWire(SchemaResult described)
        => described.Ok
            ? SchemaResult.Of(new Schema(described.Schema!.Columns
                .Select(c => c.Type is ColumnType.Date or ColumnType.Timestamp ? c with { Type = ColumnType.Text } : c)
                .ToList()))
            : described;

    /// <summary>The columns of the SQL a reader node runs over one file or glob of files.</summary>
    public static SchemaResult OfFileSql(ISchemaCatalog catalog, string kind, string path, string sql)
        => SchemaGuards.PathProblem(kind, path, allowGlob: true)
           ?? OnTheWire(catalog.FileQuerySchema(sql, [path]));
}
