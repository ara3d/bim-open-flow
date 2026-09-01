using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Keeps the named columns in the given order; unknown names warn,
/// never error.</summary>
public sealed class TableProjectNode : IFlowNode
{
    public const string Kind = "table.project";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text)],
        "Keeps the comma-separated columns, in that order; unknown names warn.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var names = parameters.RequiredText("columns", Kind).SplitNames();
        if (names.Count == 0)
            throw new ArgumentException($"{Kind}: parameter 'columns' names no columns.");

        var builder = new DataTableBuilder(table.Name);
        foreach (var name in names)
        {
            var col = table.ColumnIndex(name);
            if (col < 0)
            {
                context.Warn($"{Kind}: no column named '{name}'");
                continue;
            }
            var values = new object?[table.RowCount()];
            for (var row = 0; row < values.Length; row++)
                values[row] = table[col, row];
            builder.AddColumn(values, table.Columns[col].Descriptor.Name, table.Columns[col].Descriptor.Type);
        }
        return [new TableValue(builder.Build())];
    }
}
