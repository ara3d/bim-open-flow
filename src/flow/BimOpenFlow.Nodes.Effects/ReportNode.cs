using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.report: writes the input table as a minimal standalone HTML report at 'path'.</summary>
public sealed class ReportNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "sink.report", 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("title", ParamKind.Text),
        },
        "Writes a standalone HTML report (title + table). Outputs a one-row summary (path, rowCount).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Spec.Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        Sinks.WriteAllText(path, ReportHtml.ToHtml(parameters.GetText("title"), table));
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("report",
                ("path", path),
                ("rowCount", (long)table.Rows.Count))),
        };
    }
}
