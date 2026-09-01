using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Stacks b's rows under a's. Strict mode requires identical column
/// sequences; byName matches columns by name and null-fills the missing ones.</summary>
public sealed class TableConcatNode : IFlowNode
{
    public const string Kind = "table.concat";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("a", PortType.Table),
            new PortSpec("b", PortType.Table),
        ],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("columns", ParamKind.Enum, "strict", ["strict", "byName"])],
        "Appends b's rows after a's, matching columns strictly by position or loosely by name.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = inputs.TableInput(0, Kind);
        var b = inputs.TableInput(1, Kind);
        var mode = parameters.RequiredEnum("columns", Kind, "strict", "strict", "byName");
        var aNames = a.Names();
        var bNames = b.Names();

        if (mode == "strict" && !aNames.SequenceEqual(bNames, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"{Kind}: strict concat requires identical column sequences; " +
                $"a: [{string.Join(", ", aNames)}], b: [{string.Join(", ", bNames)}].");

        var ord = TableColumns.FreeName("__row__", a, b);
        var src = TableColumns.FreeName("__src__", a, b);
        var union = mode == "strict" ? "UNION ALL" : "UNION ALL BY NAME";
        var outNames = mode == "strict"
            ? aNames
            : aNames.Concat(bNames.Where(n => a.ColumnIndex(n) < 0)).ToList();
        var sql = $"""
            SELECT {string.Join(", ", outNames.Select(TableColumns.Ident))} FROM (
              SELECT *, 0 AS {src.Ident()} FROM a
              {union}
              SELECT *, 1 AS {src.Ident()} FROM b)
            ORDER BY {src.Ident()}, {ord.Ident()}
            """;
        var tables = new List<(string, IDataTable)>
        {
            ("a", a.WithOrdinal(ord)),
            ("b", b.WithOrdinal(ord)),
        };
        return [new TableValue(DuckTableSql.Run(tables, sql))];
    }
}
