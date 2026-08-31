using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Expressions;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Keeps the rows where the Boolean expression evaluates to true.
/// A null result excludes the row (SQL WHERE semantics).</summary>
public sealed class TableFilterNode : IFlowNode
{
    public const string Kind = "table.filter";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("expr", ParamKind.Expression)],
        "Keeps rows where the Boolean expression is true; null results exclude the row.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var bindings = table.Bindings();
        var expr = TableExpressions.Compile(Kind, "expr", parameters.RequiredText("expr", Kind), bindings);
        if (expr.Type is { } t && t != ScalarType.Boolean)
            throw new ArgumentException($"{Kind}: 'expr' must be Boolean, but it is {t}.");

        var kept = new List<int>();
        for (var row = 0; row < table.Rows.Count; row++)
            if (expr.Eval(table.RowLookup(bindings, row)) is BooleanScalar { Value: true })
                kept.Add(row);
        return [new TableValue(table.KeepRows(kept, table.Name))];
    }
}
