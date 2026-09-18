using Ara3D.BimOpenSchema.DuckDb;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Source schemas from DuckDB itself: a CSV header sniff, information_schema
/// for a table, and DESCRIBE over empty typed views for raw SQL. Every call opens and
/// closes its own session, which costs milliseconds; SchemaCache absorbs repeats.</summary>
public sealed class DuckDbCatalog(IConnectionRegistry registry) : ICatalog
{
    public SchemaResult CsvSchema(string source, string path)
        => Describe([new SourceUse(source, SourceKind.Csv, path)],
            s => s.Connection.Query($"DESCRIBE SELECT * FROM {new SourceUse(source, SourceKind.Csv, path).Ident}"));

    public SchemaResult TableSchema(string source, string table)
        => Describe([new SourceUse(source, SourceKind.Table, table)],
            s => s.Connection.Query($"DESCRIBE SELECT * FROM {new SourceUse(source, SourceKind.Table, table).Ident}"));

    public SchemaResult QuerySchema(string sql, IReadOnlyList<Schema> inputs)
        => Describe([], s =>
        {
            for (var i = 0; i < inputs.Count; i++)
                s.Connection.Execute($"CREATE VIEW t{i + 1} AS {EmptyTyped(inputs[i])}");
            return s.Connection.Query($"DESCRIBE {sql}");
        });

    private SchemaResult Describe(IReadOnlyList<SourceUse> sources, Func<DuckDbSession, Ara3D.DataTable.IDataTable> describe)
    {
        try
        {
            using var session = DuckDbSession.For(sources, registry);
            var rows = describe(session);
            var columns = new List<Column>(rows.Rows.Count);
            for (var row = 0; row < rows.Rows.Count; row++)
                columns.Add(new((string)rows[0, row], DuckDbTypes.FromDuckDb((string)rows[1, row])));
            return SchemaResult.Of(new Schema(columns));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return SchemaResult.Fail(e.Message);
        }
    }

    /// <summary>A zero-row SELECT whose columns carry the schema's types.</summary>
    private static string EmptyTyped(Schema schema)
        => schema.Columns.Count == 0
            ? "SELECT 1 AS _ WHERE FALSE"
            : "SELECT " + string.Join(", ", schema.Columns.Select(c => $"CAST(NULL AS {SqlType(c.Type)}) AS {c.Name.Ident()}")) + " WHERE FALSE";

    private static string SqlType(ColumnType type)
        => type == ColumnType.Unknown ? "VARCHAR" : type.ToSqlType();
}
