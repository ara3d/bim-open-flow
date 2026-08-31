using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportCsv: writes the input table to an RFC-4180 CSV file at 'path'.</summary>
public sealed class ExportCsvNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "sink.exportCsv", 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[] { new ParamSpec("path", ParamKind.FilePath) },
        "Writes the input table as RFC-4180 CSV (header row, invariant formatting). Outputs a one-row summary (path, rowCount).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Spec.Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        Sinks.WriteAllText(path, CsvWriting.ToCsvText(table));
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportCsv",
                ("path", path),
                ("rowCount", (long)table.Rows.Count))),
        };
    }
}
