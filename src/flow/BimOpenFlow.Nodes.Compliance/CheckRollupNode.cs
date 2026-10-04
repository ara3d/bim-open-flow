using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// check.rollup: summarizes a verdict table into one row per checkId
/// (first-appearance order) with per-verdict counts and the worst verdict
/// present (severity: Fail > NeedsReview > InfoNotAvailable > Pass).
/// </summary>
public sealed class CheckRollupNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "check.rollup", 1, NodeCapability.Pure,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        Array.Empty<ParamSpec>(),
        "Groups a verdict table by checkId into counts and the worst verdict per check.");

    private sealed class Group
    {
        public required string Title;
        public required string Citation;
        public readonly long[] Counts = new long[4];
        public Verdict Worst = Verdict.Pass;
    }

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableAt(0);
        table.RequireVerdictTable();
        var verdictColumn = table.RequireColumn(VerdictSchema.VerdictColumn);
        var idColumn = table.RequireColumn(VerdictSchema.CheckIdColumn);
        var titleColumn = table.RequireColumn(VerdictSchema.CheckTitleColumn);
        var citationColumn = table.RequireColumn(VerdictSchema.CitationColumn);

        var order = new List<string>();
        var groups = new Dictionary<string, Group>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var id = table.TextCell(idColumn, r);
            if (!groups.TryGetValue(id, out var group))
            {
                group = new Group
                {
                    Title = table.TextCell(titleColumn, r),
                    Citation = table.TextCell(citationColumn, r),
                };
                groups.Add(id, group);
                order.Add(id);
            }
            var verdict = VerdictExtensions.ParseVerdict(table.TextCell(verdictColumn, r));
            group.Counts[(int)verdict]++;
            group.Worst = group.Worst.Worst(verdict);
        }

        return new FlowValue[] { new TableValue(BuildSummary(order, groups)) };
    }

    private static MemoryTable BuildSummary(IReadOnlyList<string> order, IReadOnlyDictionary<string, Group> groups)
    {
        var n = order.Count;
        MemoryColumn Text(string name, int index, Func<string, Group, string> cell)
            => NodeTables.TextColumn(name, index, n, r => cell(order[r], groups[order[r]]));
        MemoryColumn Count(string name, int index, Verdict verdict)
        {
            var cells = new object?[n];
            for (var r = 0; r < n; r++)
                cells[r] = groups[order[r]].Counts[(int)verdict];
            return new MemoryColumn(name, typeof(long), cells, index);
        }
        return new MemoryTable("rollup", new[]
        {
            Text(VerdictSchema.CheckIdColumn, 0, (id, _) => id),
            Text(VerdictSchema.CheckTitleColumn, 1, (_, g) => g.Title),
            Text(VerdictSchema.CitationColumn, 2, (_, g) => g.Citation),
            Count("passCount", 3, Verdict.Pass),
            Count("failCount", 4, Verdict.Fail),
            Count("needsReviewCount", 5, Verdict.NeedsReview),
            Count("infoNotAvailableCount", 6, Verdict.InfoNotAvailable),
            Text("worst", 7, (_, g) => g.Worst.ToText()),
        });
    }
}
