using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Expressions;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// check.rule: evaluates a Boolean expression per input row. True is Pass;
/// false is Fail (or NeedsReview where reviewExpr is true); a null result
/// (missing data) is InfoNotAvailable. Output = input columns + verdict columns.
/// </summary>
public sealed class CheckRuleNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "check.rule", 1, NodeCapability.Pure,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("checkId", ParamKind.Text),
            new ParamSpec("title", ParamKind.Text),
            new ParamSpec("citation", ParamKind.Text),
            new ParamSpec("expr", ParamKind.Expression),
            new ParamSpec("reviewExpr", ParamKind.Expression),
        },
        "Per row: expr true = Pass, false = Fail (NeedsReview where reviewExpr is true), null = InfoNotAvailable.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableAt(0);
        var expr = table.CompileBoolean(parameters.GetText("expr"), "expr");
        var reviewText = parameters.GetText("reviewExpr");
        var review = reviewText.Length == 0 ? null : table.CompileBoolean(reviewText, "reviewExpr");
        var columns = table.ColumnIndexMap();
        var verdicts = new Verdict[table.Rows.Count];
        for (var i = 0; i < verdicts.Length; i++)
        {
            var lookup = table.RowLookup(columns, i);
            verdicts[i] = expr.Eval(lookup) switch
            {
                null => Verdict.InfoNotAvailable,
                BooleanScalar { Value: true } => Verdict.Pass,
                _ => review?.Eval(lookup) is BooleanScalar { Value: true } ? Verdict.NeedsReview : Verdict.Fail,
            };
        }
        return new FlowValue[]
        {
            new TableValue(table.WithVerdicts(verdicts,
                parameters.GetText("checkId"), parameters.GetText("title"), parameters.GetText("citation"))),
        };
    }
}
