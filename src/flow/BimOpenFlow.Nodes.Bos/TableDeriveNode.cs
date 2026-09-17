using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Appends one computed column to the input table. The column's .NET type comes
/// from the expression's static type; rows where the expression is null get a null cell.</summary>
public sealed class TableDeriveNode : IFlowNode
{
    public const string Kind = "table.derive";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("name", ParamKind.Text),
            new ParamSpec("expr", ParamKind.Expression),
        ],
        "Outputs the input table plus one computed column.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var name = parameters.RequiredText("name", Kind);
        if (table.Columns.Any(c => c.Descriptor.Name == name))
            throw new ArgumentException($"{Kind}: column '{name}' already exists.");

        var bindings = table.Bindings();
        var expr = TableExpressions.Compile(Kind, "expr", parameters.RequiredText("expr", Kind), bindings);
        var type = expr.Type
            ?? throw new ArgumentException($"{Kind}: 'expr' is always null; its column type cannot be inferred.");

        var values = new object?[table.Rows.Count];
        for (var row = 0; row < values.Length; row++)
            values[row] = expr.Eval(table.RowLookup(bindings, row)).ToCell();

        var builder = new DataTableBuilder(table.Name);
        foreach (var c in table.Columns)
            builder.AddColumn(c);
        builder.AddColumn(values, name, type.ToNetType());
        return [new TableValue(builder.Build())];
    }
}
