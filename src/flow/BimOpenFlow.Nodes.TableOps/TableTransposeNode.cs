using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Rows become columns: the header column's values name the new columns
/// and every other column becomes a row. All value cells widen to text. Intended
/// for small summary tables; errors above 1,000 rows.</summary>
public sealed class TableTransposeNode : IFlowNode
{
    public const string Kind = "table.transpose";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("headerColumn", ParamKind.Text)],
        "Turns rows into columns, using the header column's values as the new column names.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        if (table.RowCount() > 1000)
            throw new ArgumentException(
                $"{Kind}: table has {table.RowCount()} rows; transpose is limited to 1000.");
        if (table.Columns.Count == 0)
            throw new ArgumentException($"{Kind}: table has no columns.");

        var headerName = parameters.GetText("headerColumn");
        var header = headerName.Length == 0 ? 0 : table.RequireColumn(headerName, Kind);
        var others = table.Columns.Where(c => c.ColumnIndex != header).ToList();

        var headers = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { table.Columns[header].Descriptor.Name };
        for (var row = 0; row < table.RowCount(); row++)
        {
            var text = TableColumns.CellText(table[header, row])
                ?? throw new ArgumentException($"{Kind}: header value in row {row} is null.");
            if (!seen.Add(text))
                throw new ArgumentException($"{Kind}: duplicate header value '{text}'.");
            headers.Add(text);
        }

        var builder = new DataTableBuilder(table.Name);
        builder.AddColumn(others.Select(c => (object?)c.Descriptor.Name).ToArray(),
            table.Columns[header].Descriptor.Name, typeof(string));
        for (var row = 0; row < headers.Count; row++)
            builder.AddColumn(
                others.Select(c => (object?)TableColumns.CellText(table[c.ColumnIndex, row])).ToArray(),
                headers[row], typeof(string));
        return [new TableValue(builder.Build())];
    }
}
