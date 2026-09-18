using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Parsing;

namespace BimOpenFlow.Relations;

/// <summary>Fluent construction so a plan reads as a pipeline:
/// <c>Plans.Csv("files", "walls.csv").Filter("[height] > 3").Select("id", "height")</c>.</summary>
public static class Plans
{
    public static ReadCsv Csv(string source, string path) => new(source, path);
    public static ReadTable Table(string source, string table) => new(source, table);
    public static RawSql Sql(string sql, params Plan[] inputs) => new(sql, inputs);
    public static Union Union(params Plan[] inputs) => new(inputs);

    public static Select Select(this Plan input, params string[] columns) => new(input, columns);
    public static Rename Rename(this Plan input, params Renaming[] renamings) => new(input, renamings);
    public static Rename Rename(this Plan input, string from, string to) => new(input, [new(from, to)]);
    public static Cast Cast(this Plan input, string column, ColumnType type) => new(input, column, type);
    public static Derive Derive(this Plan input, string name, Expr expression) => new(input, name, expression);
    public static Derive Derive(this Plan input, string name, string expression) => new(input, name, expression.ParseExpr());
    public static Filter Filter(this Plan input, Expr predicate) => new(input, predicate);
    public static Filter Filter(this Plan input, string predicate) => new(input, predicate.ParseExpr());
    public static Distinct Distinct(this Plan input) => new(input);
    public static Sort Sort(this Plan input, params SortKey[] keys) => new(input, keys);
    public static Sort SortBy(this Plan input, string column, bool descending = false) => new(input, [new(column, descending)]);
    public static Limit Limit(this Plan input, long count, long offset = 0) => new(input, count, offset);
    public static Join Join(this Plan left, Plan right, JoinKind kind, params JoinKey[] keys) => new(left, right, keys, kind);
    public static Join Join(this Plan left, Plan right, string leftKey, string rightKey, JoinKind kind = JoinKind.Inner)
        => new(left, right, [new(leftKey, rightKey)], kind);
    public static Aggregate Aggregate(this Plan input, IReadOnlyList<string> groupBy, params Aggregation[] aggregates)
        => new(input, groupBy, aggregates);

    /// <summary>Parses expression text, throwing on any parse error. Type checking happens in the Schema layer.</summary>
    public static Expr ParseExpr(this string text)
    {
        var parsed = Expression.Parse(text);
        return parsed.Success
            ? parsed.Root!
            : throw new ArgumentException($"Invalid expression '{text}': {string.Join("; ", parsed.Errors)}");
    }
}
