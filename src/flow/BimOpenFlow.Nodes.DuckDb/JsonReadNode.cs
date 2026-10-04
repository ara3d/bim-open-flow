using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Reads a JSON file into a table via DuckDB read_json. Layout picks
/// the file shape (array of records vs newline-delimited); flatten expands one
/// level of nested objects into dotted columns.</summary>
public sealed class JsonReadNode : IFlowNode
{
    public const string Kind = "json.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("layout", ParamKind.Enum, "auto", ["auto", "records", "lines"]),
            new ParamSpec("flatten", ParamKind.Boolean, "false"),
        ],
        "Reads a JSON file (record array or newline-delimited) into a table, optionally flattening one level of nested objects into dotted columns.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        var files = FileReadCache.ResolveFiles(path, Kind);
        var format = parameters.GetText("layout", "auto") switch
        {
            "auto" => "auto",
            "records" => "array",
            "lines" => "newline_delimited",
            var l => throw new ArgumentException($"{Kind}: unknown layout '{l}'."),
        };
        var flatten = parameters.GetBoolean("flatten");
        var table = FileReadCache.GetOrLoad(
            FileReadCache.CacheKey(Kind, files, $"format={format},flatten={flatten}"),
            () => Load(path, format, flatten));
        return [new TableValue(table)];
    }

    private static IDataTable Load(string path, string format, bool flatten)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        using var conn = BosDuckDb.OpenInMemory();
        conn.Execute($"CREATE VIEW src AS SELECT * FROM read_json('{path.ToSqlLiteral()}', format='{format}')");
        var sql = flatten ? $"SELECT {string.Join(", ", FlattenedColumns(conn))} FROM src" : "SELECT * FROM src";
        return conn.Query(sql, name).NormalizeDatesToText();
    }

    /// <summary>One projection term per output column: struct columns expand one
    /// level to "column.field", everything else passes through unchanged.</summary>
    private static IEnumerable<string> FlattenedColumns(DuckDBConnection conn)
    {
        var columns = conn.Query(
            "SELECT column_name, data_type FROM information_schema.columns "
            + "WHERE table_name = 'src' ORDER BY ordinal_position");
        foreach (var row in columns.Rows)
        {
            var column = row[0]?.ToString() ?? "";
            var type = row[1]?.ToString() ?? "";
            var ident = DuckTableSql.QuoteIdent(column);
            if (!type.StartsWith("STRUCT(", StringComparison.OrdinalIgnoreCase))
            {
                yield return ident;
                continue;
            }
            // A zero-row unnest still binds the schema, giving the field names without parsing the type.
            var fields = conn.Query($"SELECT unnest({ident}, recursive := false) FROM src LIMIT 0");
            foreach (var field in fields.Columns.Select(c => c.Descriptor.Name))
                yield return $"{ident}.{DuckTableSql.QuoteIdent(field)} AS {DuckTableSql.QuoteIdent($"{column}.{field}")}";
        }
    }
}
