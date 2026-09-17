using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz;

/// <summary>Names and optionally projects a table for the table pane — a
/// pinned, titled view rather than transient click-a-node inspection.</summary>
// TODO: the columns projection duplicates table.project (Nodes.Tables) — decide
// whether view.table should delegate to a shared Support helper or table.project
// should retire in favor of this node (core proposal Set 3/4 overlap).
public sealed class ViewTableNode : IFlowNode
{
    public const string Kind = "view.table";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("title", ParamKind.Text),
            new ParamSpec("columns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
        ],
        "Titles a table view; comma-separated 'columns' optionally projects "
        + "(default all, kept in the named order; unknown names warn).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var names = parameters.GetText("columns").SplitNames();
        return names.Count == 0
            ? [new TableValue(table)]
            : [new TableValue(VizProjection.Project(table,
                VizProjection.ResolveColumns(context, table, names, Kind)))];
    }
}
