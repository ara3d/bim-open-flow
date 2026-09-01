using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Renames columns via comma-separated old=new pairs. Unknown old names
/// warn and are skipped; a new name colliding with a remaining column errors.</summary>
public sealed class TableRenameNode : IFlowNode
{
    public const string Kind = "table.rename";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("renames", ParamKind.Text)],
        "Renames columns using comma-separated 'old=new' pairs.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in parameters.RequiredText("renames", Kind).SplitNames())
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                throw new ArgumentException($"{Kind}: cannot parse rename '{entry}' (expected old=new).");
            if (table.ColumnIndex(parts[0]) < 0)
            {
                context.Warn($"{Kind}: no column named '{parts[0]}'; rename skipped.");
                continue;
            }
            if (!renames.TryAdd(table.CanonicalName(parts[0], Kind), parts[1]))
                throw new ArgumentException($"{Kind}: column '{parts[0]}' is renamed more than once.");
        }

        var finalNames = table.Names()
            .Select(n => renames.TryGetValue(n, out var renamed) ? renamed : n).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in finalNames)
            if (!seen.Add(name))
                throw new ArgumentException($"{Kind}: renaming produces duplicate column '{name}'.");

        var terms = table.Names().Zip(finalNames,
            (old, final) => old == final ? old.Ident() : $"{old.Ident()} AS {final.Ident()}");
        return [new TableValue(DuckTableSql.Run(table, $"SELECT {string.Join(", ", terms)} FROM t"))];
    }
}
