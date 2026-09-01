using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Splits one text column on a separator into new columns, one per
/// requested name. Fewer parts than names yields nulls; extra parts are dropped.</summary>
public sealed class TableSplitColumnNode : IFlowNode
{
    public const string Kind = "table.splitColumn";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text),
            new ParamSpec("separator", ParamKind.Text, "-"),
            new ParamSpec("names", ParamKind.Text),
            new ParamSpec("keep", ParamKind.Boolean, "false"),
        ],
        "Splits a column on a separator into new columns named by 'names'.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = table.CanonicalName(parameters.RequiredText("column", Kind), Kind);
        var separator = parameters.GetText("separator", "-");
        if (separator.Length == 0)
            throw new ArgumentException($"{Kind}: parameter 'separator' must not be empty.");
        var names = parameters.RequiredText("names", Kind).SplitNames();
        var keep = parameters.GetBoolean("keep");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (table.ColumnIndex(name) >= 0)
                throw new ArgumentException($"{Kind}: new column '{name}' already exists.");
            if (!seen.Add(name))
                throw new ArgumentException($"{Kind}: new column '{name}' is named more than once.");
        }

        var sep = separator.Literal();
        var parts = names.Select((name, i) =>
            $"CASE WHEN len(string_split({column.Ident()}, {sep})) >= {i + 1} " +
            $"THEN split_part({column.Ident()}, {sep}, {i + 1}) END AS {name.Ident()}");
        var source = keep ? "*" : $"* EXCLUDE ({column.Ident()})";
        var sql = $"SELECT {source}, {string.Join(", ", parts)} FROM t";
        return [new TableValue(TableColumns.RunSql(Kind, table, sql))];
    }
}
