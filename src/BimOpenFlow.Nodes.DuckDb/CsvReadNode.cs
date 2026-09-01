using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Reads a CSV file (or glob of files) into a table via DuckDB
/// read_csv with typed options. A glob unions all matching files and appends a
/// filename column so provenance survives the union.</summary>
public sealed class CsvReadNode : IFlowNode
{
    public const string Kind = "csv.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("delimiter", ParamKind.Text, ","),
            new ParamSpec("header", ParamKind.Boolean, "true"),
            new ParamSpec("skipRows", ParamKind.Integer, "0"),
            new ParamSpec("quote", ParamKind.Text, "\""),
            new ParamSpec("nullText", ParamKind.Text, ""),
            new ParamSpec("encoding", ParamKind.Enum, "utf8", ["utf8", "utf16", "latin1"]),
            new ParamSpec("inferTypes", ParamKind.Boolean, "true"),
        ],
        "Reads a CSV file or glob of files into a table, with typed delimiter, header, skip, quote, null-text, and encoding options.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        var files = FileReadCache.ResolveFiles(path, Kind);
        var header = parameters.GetBoolean("header", true);
        var options = Options(parameters, FileReadCache.IsGlob(path), header);
        var table = FileReadCache.GetOrLoad(
            FileReadCache.CacheKey(Kind, files, options),
            () => Load(path, options, header));
        return [new TableValue(table)];
    }

    private static string Options(ParamValues parameters, bool isGlob, bool header)
    {
        var encoding = parameters.GetText("encoding", "utf8") switch
        {
            "utf8" => "utf-8",
            "utf16" => "utf-16",
            "latin1" => "latin-1",
            var e => throw new ArgumentException($"{Kind}: unknown encoding '{e}'."),
        };
        var options =
            $", delim={DuckTableSql.QuoteLiteral(parameters.GetText("delimiter", ","))}"
            + $", quote={DuckTableSql.QuoteLiteral(parameters.GetText("quote", "\""))}"
            + $", nullstr={DuckTableSql.QuoteLiteral(parameters.GetText("nullText"))}"
            + $", header={(header ? "true" : "false")}"
            + $", skip={parameters.GetInteger("skipRows")}"
            + $", encoding='{encoding}'";
        if (!parameters.GetBoolean("inferTypes", true))
            options += ", all_varchar=true";
        if (isGlob)
            options += ", filename=true";
        return options;
    }

    private static IDataTable Load(string path, string options, bool header)
    {
        using var conn = BosDuckDb.OpenInMemory();
        var table = conn.Query($"SELECT * FROM read_csv('{path.ToSqlLiteral()}'{options})",
            Path.GetFileNameWithoutExtension(path)).NormalizeDates();
        return header ? table : table.RenameColumns(DefaultColumnName);
    }

    /// <summary>Headerless files get DuckDB's column0..N-1 names; the spec says Column1..N.</summary>
    private static string DefaultColumnName(string name)
        => name.StartsWith("column", StringComparison.Ordinal)
           && int.TryParse(name.AsSpan("column".Length), out var i)
            ? $"Column{i + 1}"
            : name;
}
