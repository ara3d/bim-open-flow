using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Keeps the named columns in the given order; <c>column as Alias</c> renames one.
/// Unknown names warn, never error. A duplicate output name is an error.</summary>
public sealed class TableProjectNode : IFlowNode
{
    public const string Kind = "table.project";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true)],
        "Keeps the comma-separated columns, in that order; 'column as Alias' renames one; unknown names warn.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var terms = Projection.ParseAll(parameters.RequiredText("columns", Kind).SplitNames());
        if (terms.Count == 0)
            throw new ArgumentException($"{Kind}: parameter 'columns' names no columns.");

        var kept = terms.Select(term => (term, col: table.ColumnIndex(term.Column))).ToList();
        foreach (var (term, _) in kept.Where(k => k.col < 0))
            context.Warn($"{Kind}: no column named '{term.Column}'");
        kept = kept.Where(k => k.col >= 0).ToList();
        if (Projection.FirstDuplicate(kept.Select(k => k.term.Alias ?? table.Columns[k.col].Descriptor.Name)) is { } clash)
            throw new ArgumentException($"{Kind}: the output has two columns named '{clash}'.");

        var builder = new DataTableBuilder(table.Name);
        foreach (var (term, col) in kept)
        {
            var values = new object?[table.RowCount()];
            for (var row = 0; row < values.Length; row++)
                values[row] = table[col, row];
            builder.AddColumn(values, term.Alias ?? table.Columns[col].Descriptor.Name, table.Columns[col].Descriptor.Type);
        }
        return [new TableValue(builder.Build())];
    }
}
