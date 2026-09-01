using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportParquet: writes the input table to a Parquet file via DuckDB COPY TO.</summary>
public sealed class ExportParquetNode : IFlowNode
{
    public const string Kind = "sink.exportParquet";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("compression", ParamKind.Enum, "zstd", new[] { "zstd", "snappy", "none" }),
        },
        "Writes the input table as a Parquet file (zstd, snappy, or uncompressed). Outputs a one-row summary (path, rowCount).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        var compression = parameters.GetEnum("compression", Kind, "zstd", "zstd", "snappy", "none") switch
        {
            "snappy" => "SNAPPY",
            "none" => "UNCOMPRESSED",
            _ => "ZSTD",
        };
        Sinks.ReplaceVia(path, temp =>
            DuckWriting.CopyTable(table, temp, $"FORMAT PARQUET, COMPRESSION {compression}"));
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportParquet",
                ("path", path),
                ("rowCount", (long)table.Rows.Count))),
        };
    }
}
