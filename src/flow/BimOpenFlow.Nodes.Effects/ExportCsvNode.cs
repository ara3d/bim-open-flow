using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportCsv: writes the input table to an RFC-4180 CSV file at 'path'.</summary>
public sealed class ExportCsvNode : IFlowNode
{
    public const string Kind = "sink.exportCsv";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("delimiter", ParamKind.Text, ","),
            new ParamSpec("header", ParamKind.Boolean, "true"),
        },
        "Writes the input table as RFC-4180 CSV (invariant formatting; configurable delimiter, optional header row). Outputs a one-row summary (path, rowCount).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        var delimiter = parameters.GetText("delimiter", ",");
        if (delimiter.Length == 0)
            throw new ArgumentException($"{Kind}: parameter 'delimiter' must be non-empty");
        var header = parameters.GetBoolean("header", true);
        Sinks.WriteAllText(path, CsvWriting.ToCsvText(table, delimiter, header));
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportCsv",
                ("path", path),
                ("rowCount", (long)table.Rows.Count))),
        };
    }
}
