using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// check.required: verifies named columns are present and non-null per row.
/// Any listed column missing from the table = every row InfoNotAvailable;
/// a null cell in a listed column = that row Fail; otherwise Pass.
/// Data absence is reported, never skipped.
/// </summary>
public sealed class CheckRequiredNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "check.required", 1, NodeCapability.Pure,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("checkId", ParamKind.Text),
            new ParamSpec("title", ParamKind.Text),
            new ParamSpec("citation", ParamKind.Text),
            new ParamSpec("columns", ParamKind.Text),
        },
        "Required data check: missing column = InfoNotAvailable everywhere; null cell = Fail; else Pass.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableAt(0);
        var names = ParseColumnNames(parameters.GetText("columns"));
        var map = table.ColumnIndexMap();
        var missing = names.Where(n => !map.ContainsKey(n)).ToList();
        var verdicts = new Verdict[table.Rows.Count];
        if (missing.Count > 0)
        {
            context.Warn($"check.required: columns not present: {string.Join(", ", missing)}");
            Array.Fill(verdicts, Verdict.InfoNotAvailable);
        }
        else
        {
            for (var i = 0; i < verdicts.Length; i++)
                verdicts[i] = names.Any(n => table.Cell(map[n], i) is null) ? Verdict.Fail : Verdict.Pass;
        }
        return new FlowValue[]
        {
            new TableValue(table.WithVerdicts(verdicts,
                parameters.GetText("checkId"), parameters.GetText("title"), parameters.GetText("citation"))),
        };
    }

    private static IReadOnlyList<string> ParseColumnNames(string columns)
    {
        var names = columns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return names.Length > 0
            ? names
            : throw new ArgumentException("check.required needs at least one column name in 'columns'");
    }
}
