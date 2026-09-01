using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Generates one numeric column from start to stop by step, inclusive
/// of stop when a step lands exactly on it (generate_series semantics).</summary>
public sealed class TableRangeNode : IFlowNode
{
    public const string Kind = "table.range";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("name", ParamKind.Text, "value"),
            new ParamSpec("start", ParamKind.Number, "0"),
            new ParamSpec("stop", ParamKind.Number),
            new ParamSpec("step", ParamKind.Number, "1"),
        ],
        "Generates one numeric column from start to stop (inclusive when a step lands on it) by step; a negative step counts down.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var name = parameters.GetText("name").Trim() is { Length: > 0 } n ? n : "value";
        var start = parameters.GetNumber("start");
        var stop = parameters.RequiredNumber("stop", Kind);
        var step = parameters.GetNumber("step", 1);
        if (step == 0)
            throw new ArgumentException($"{Kind}: 'step' must not be zero.");

        var count = Math.Max(0, (long)Math.Floor((stop - start) / step + 1e-9) + 1);
        var cells = new object?[count];
        for (var i = 0L; i < count; i++)
            cells[i] = start + i * step;
        var builder = new DataTableBuilder("range");
        builder.AddColumn(cells, name, typeof(double));
        return [new TableValue(builder.Build())];
    }
}
