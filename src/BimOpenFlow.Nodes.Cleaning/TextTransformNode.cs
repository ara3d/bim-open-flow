using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Cleaning;

/// <summary>Applies trim/upper/lower/normalizeSpace in place to the named text
/// columns (empty = every text column) — the standard pre-join whitespace fix.</summary>
public sealed class TextTransformNode : IFlowNode
{
    public const string Kind = "text.transform";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("columns", ParamKind.Text),
            new ParamSpec("op", ParamKind.Enum, "trim", ["trim", "upper", "lower", "normalizeSpace"]),
        ],
        "Applies trim/upper/lower/normalizeSpace to 'columns' (empty = every text column) in place.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var named = parameters.GetText("columns").SplitNames()
            .Select(n => table.RequireTextColumn(n, Kind).Descriptor.Name).ToList();
        var targets = (named.Count > 0
            ? named
            : table.Columns.Where(c => c.IsText()).Select(c => c.Descriptor.Name))
            .ToHashSet(StringComparer.Ordinal);
        var op = parameters.RequiredEnum("op", Kind, "trim", "trim", "upper", "lower", "normalizeSpace");
        var ordinal = table.OrdinalName();
        var select = string.Join(", ", table.Columns.Select(c =>
        {
            var name = DuckTableSql.QuoteIdent(c.Descriptor.Name);
            return targets.Contains(c.Descriptor.Name)
                ? $"{Transformed(name)} AS {name}"
                : name;
        }));
        return [new TableValue(table.WithOrdinal(ordinal).RunSql(
            $"SELECT {select} FROM t ORDER BY {DuckTableSql.QuoteIdent(ordinal)}", Kind))];

        string Transformed(string c)
            => op switch
            {
                "upper" => $"upper({c})",
                "lower" => $"lower({c})",
                "normalizeSpace" => $@"regexp_replace(trim({c}), '\s+', ' ', 'g')",
                _ => $"trim({c})",
            };
    }
}
