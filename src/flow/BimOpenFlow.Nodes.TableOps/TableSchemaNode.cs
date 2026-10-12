using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>The table's shape as a table — one row per column with its name,
/// wire type (Boolean/Integer/Number/Text), and position.</summary>
public sealed class TableSchemaNode : IFlowNode
{
    public const string Kind = "table.schema";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("schema", PortType.Table)],
        Params: [],
        "Outputs the table's columns as a table: name, type, and index.",
        Schemas: [SchemaSpec.Static("schema", Shape)]);

    /// <summary>The output columns, whatever the input; none is ever null.</summary>
    private static readonly Schema Shape = new(
    [
        new Column("column", ColumnType.Text, Nullable: false),
        new Column("type", ColumnType.Text, Nullable: false),
        new Column("index", ColumnType.Integer, Nullable: false),
    ]);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var builder = new DataTableBuilder("schema");
        builder.AddColumn(table.Columns.Select(c => (object?)c.Descriptor.Name).ToArray(),
            "column", typeof(string));
        builder.AddColumn(table.Columns.Select(c => (object?)TableColumns.KindName(c.Descriptor.Type)).ToArray(),
            "type", typeof(string));
        builder.AddColumn(table.Columns.Select((_, i) => (object?)(long)i).ToArray(),
            "index", typeof(long));
        return [new TableValue(builder.Build())];
    }
}
