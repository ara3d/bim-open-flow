using System.Globalization;

namespace BimOpenFlow.Relations;

/// <summary>The canonical s-expression form of a plan. It is the plan's identity
/// (see <see cref="Plan"/>) and doubles as its debug string.</summary>
public static class PlanText
{
    public static string Render(this Plan plan)
        => plan switch
        {
            ReadCsv r => $"(csv {Q(r.Source)} {Q(r.Path)})",
            ReadTable r => $"(table {Q(r.Source)} {Q(r.Table)})",
            RawSql r => $"(sql {Q(r.Sql)}{List(r.Inputs.Select(Render))})",
            Select s => $"(select {s.Input.Render()}{List(s.Columns.Select(Q))})",
            Rename r => $"(rename {r.Input.Render()}{List(r.Renamings.Select(x => $"{Q(x.From)}>{Q(x.To)}"))})",
            Cast c => $"(cast {c.Input.Render()} {Q(c.Column)} {c.Type})",
            Derive d => $"(derive {d.Input.Render()} {Q(d.Name)} {Q(d.Expression.Render())})",
            Filter f => $"(filter {f.Input.Render()} {Q(f.Predicate.Render())})",
            Distinct d => $"(distinct {d.Input.Render()})",
            Sort s => $"(sort {s.Input.Render()}{List(s.Keys.Select(k => Q(k.Column) + (k.Descending ? " desc" : " asc")))})",
            Limit l => $"(limit {l.Input.Render()} {N(l.Count)} {N(l.Offset)})",
            Join j => $"(join {j.Kind} {j.Left.Render()} {j.Right.Render()}{List(j.Keys.Select(k => $"{Q(k.Left)}={Q(k.Right)}"))})",
            Union u => $"(union{List(u.Inputs.Select(Render))})",
            Aggregate a => $"(aggregate {a.Input.Render()}{List(a.GroupBy.Select(Q))}{List(a.Aggregates.Select(Agg))})",
            _ => throw new ArgumentException($"Unknown plan node {plan.GetType().Name}", nameof(plan)),
        };

    private static string Agg(Aggregation a)
        => $"{a.Function}({(a.Column is null ? "*" : Q(a.Column))})>{Q(a.Name)}";

    private static string List(IEnumerable<string> items)
        => $" [{string.Join(" ", items)}]";

    private static string N(long value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string Q(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
