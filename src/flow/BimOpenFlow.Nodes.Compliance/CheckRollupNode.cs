using Ara3D.BimOpenSchema;
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
        "Groups a verdict table by CheckId into counts and the worst verdict per check.",
        Schemas:
        [
            KindSchemas.Static("out",
                ColumnKinds.CheckId.NotNull(), ColumnKinds.CheckTitle.NotNull(), ColumnKinds.Citation.NotNull(),
                KindColumn.Plain(PassCount, ColumnType.Integer, nullable: false),
                KindColumn.Plain(FailCount, ColumnType.Integer, nullable: false),
                KindColumn.Plain(NeedsReviewCount, ColumnType.Integer, nullable: false),
                KindColumn.Plain(InfoNotAvailableCount, ColumnType.Integer, nullable: false),
                KindColumn.Plain(Worst, ColumnType.Text, nullable: false)),
        ]);

    private const string PassCount = "passCount", FailCount = "failCount", NeedsReviewCount = "needsReviewCount",
        InfoNotAvailableCount = "infoNotAvailableCount", Worst = "worst";

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
        var verdictColumn = table.RequireColumn(IssueColumns.Verdict);
        var idColumn = table.RequireColumn(IssueColumns.CheckId);
        var titleColumn = table.RequireColumn(IssueColumns.CheckTitle);
        var citationColumn = table.RequireColumn(IssueColumns.Citation);

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
            Text(IssueColumns.CheckId, 0, (id, _) => id),
            Text(IssueColumns.CheckTitle, 1, (_, g) => g.Title),
            Text(IssueColumns.Citation, 2, (_, g) => g.Citation),
            Count(PassCount, 3, Verdict.Pass),
            Count(FailCount, 4, Verdict.Fail),
            Count(NeedsReviewCount, 5, Verdict.NeedsReview),
            Count(InfoNotAvailableCount, 6, Verdict.InfoNotAvailable),
            Text(Worst, 7, (_, g) => g.Worst.ToText()),
        });
    }
}
