using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

/// <summary>Evaluates a DuckDB reader with a Database value wired into its bindable path parameter,
/// as the engine does for an edge into <c>node.path</c>.</summary>
internal static class BoundPath
{
    public static IDataTable EvalTable(this IFlowNode node, DatabaseValue source, params (string Name, string Value)[] ps)
        => EvalWithWarnings(node, source, ps).Table;

    public static (IDataTable Table, IReadOnlyList<string> Warnings) EvalWithWarnings(
        this IFlowNode node, DatabaseValue source, params (string Name, string Value)[] ps)
    {
        var context = new FakeEvalContext();
        var values = ps.ToDictionary(p => p.Name, p => p.Value);
        var bound = new Dictionary<string, FlowValue> { ["path"] = source };
        var output = node.Eval(context, [], new ParamValues(values, bound))[0];
        return (((TableValue)output).Table, context.Warnings);
    }
}
