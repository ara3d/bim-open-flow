using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Reads a Parquet file (or glob of files) into a table via DuckDB
/// read_parquet. Parquet is self-describing, so path is the only parameter.</summary>
public sealed class ParquetReadNode : ISchemaNode
{
    public const string Kind = "parquet.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("path", ParamKind.FilePath, Bindable: PortType.Text, Suggest: SuggestSource.FilesWith(".parquet"))],
        "Reads a Parquet file or glob of files into a table using DuckDB read_parquet.",
        Schemas: [SchemaSpec.Inferred("table")]);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaGuards.Guard(() =>
        {
            var path = parameters.GetText("path");
            return DuckSchemas.OfFileSql(catalog, Kind, path, ReadSql(path));
        });

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        var files = FileReadCache.ResolveFiles(path, Kind);
        var table = FileReadCache.GetOrLoad(
            FileReadCache.CacheKey(Kind, files, ""),
            () => Load(path));
        return [new TableValue(table)];
    }

    private static string ReadSql(string path)
        => $"SELECT * FROM read_parquet('{path.ToSqlLiteral()}')";

    private static IDataTable Load(string path)
    {
        using var conn = BosDuckDb.OpenInMemory();
        return conn.Query(ReadSql(path),
            Path.GetFileNameWithoutExtension(path)).NormalizeDatesToText();
    }
}
