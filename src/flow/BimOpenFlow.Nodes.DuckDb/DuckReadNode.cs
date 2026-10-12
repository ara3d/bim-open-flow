using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Loads one data file (csv/parquet/json) into a table via DuckDB's
/// readers, cached by file content hash so unchanged files never reload.</summary>
public sealed class DuckReadNode : ISchemaNode
{
    public const string Kind = "duck.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath, Bindable: PortType.Text, Suggest: SuggestSource.FilesWith(".csv", ".parquet", ".json")),
            new ParamSpec("format", ParamKind.Enum, "auto", ["auto", "csv", "parquet", "json"]),
        ],
        "Loads a CSV, Parquet, or JSON file into a table using DuckDB's readers.",
        Schemas: [SchemaSpec.Inferred("table")]);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaGuards.Guard(() =>
        {
            var path = parameters.GetText("path");
            return SchemaGuards.PathProblem(Kind, path)
                   ?? DuckSchemas.OfFileSql(catalog, Kind, path, ReadSql(path, ReaderFunction(path, parameters.GetText("format", "auto"))));
        });

    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var reader = ReaderFunction(path, parameters.GetText("format", "auto"));
        var table = Cache.GetOrAdd($"{ContentHash(path)}:{reader}", _ => Load(path, reader));
        return [new TableValue(table)];
    }

    private static string ReaderFunction(string path, string format)
        => format switch
        {
            "csv" => "read_csv_auto",
            "parquet" => "read_parquet",
            "json" => "read_json_auto",
            "auto" => Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".csv" => "read_csv_auto",
                ".parquet" => "read_parquet",
                ".json" => "read_json_auto",
                var ext => throw new ArgumentException(
                    $"{Kind}: cannot infer format from extension '{ext}'; set the format parameter."),
            },
            _ => throw new ArgumentException($"{Kind}: unknown format '{format}'."),
        };

    private static string ReadSql(string path, string reader)
        => $"SELECT * FROM {reader}('{path.ToSqlLiteral()}')";

    private static IDataTable Load(string path, string reader)
    {
        using var conn = BosDuckDb.OpenInMemory();
        return conn.Query(ReadSql(path, reader), Path.GetFileNameWithoutExtension(path))
            .NormalizeDatesToText();
    }

    private static string ContentHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
