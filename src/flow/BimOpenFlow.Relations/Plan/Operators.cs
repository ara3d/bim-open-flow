using Ara3D.DataFlowEngine.Expressions.Parsing;

namespace BimOpenFlow.Relations;

public readonly record struct Renaming(string From, string To);
public readonly record struct SortKey(string Column, bool Descending = false);
public readonly record struct JoinKey(string Left, string Right);
public enum JoinKind { Inner, Left, Right, Full, Semi, Anti }
public enum AggregateFunction { Count, Sum, Min, Max, Avg }

/// <summary>One aggregate column. Column is null only for Count, meaning count(*).</summary>
public readonly record struct Aggregation(AggregateFunction Function, string? Column, string Name);

public abstract class UnaryPlan(Plan input) : Plan
{
    public Plan Input => input;
    public override IReadOnlyList<Plan> Inputs => [input];
}

public sealed class Select(Plan input, IReadOnlyList<string> columns) : UnaryPlan(input)
{
    public IReadOnlyList<string> Columns => columns;
}

public sealed class Rename(Plan input, IReadOnlyList<Renaming> renamings) : UnaryPlan(input)
{
    public IReadOnlyList<Renaming> Renamings => renamings;
}

public sealed class Cast(Plan input, string column, ColumnType type) : UnaryPlan(input)
{
    public string Column => column;
    public ColumnType Type => type;
}

public sealed class Derive(Plan input, string name, Expr expression) : UnaryPlan(input)
{
    public string Name => name;
    public Expr Expression => expression;
}

public sealed class Filter(Plan input, Expr predicate) : UnaryPlan(input)
{
    public Expr Predicate => predicate;
}

public sealed class Distinct(Plan input) : UnaryPlan(input);

public sealed class Sort(Plan input, IReadOnlyList<SortKey> keys) : UnaryPlan(input)
{
    public IReadOnlyList<SortKey> Keys => keys;
}

public sealed class Limit(Plan input, long count, long offset = 0) : UnaryPlan(input)
{
    public long Count => count;
    public long Offset => offset;
}

public sealed class Join(Plan left, Plan right, IReadOnlyList<JoinKey> keys, JoinKind kind = JoinKind.Inner) : Plan
{
    public Plan Left => left;
    public Plan Right => right;
    public IReadOnlyList<JoinKey> Keys => keys;
    public JoinKind Kind => kind;
    public override IReadOnlyList<Plan> Inputs => [left, right];
}

/// <summary>Union by position; every input must have the same schema.</summary>
public sealed class Union(IReadOnlyList<Plan> inputs) : Plan
{
    public override IReadOnlyList<Plan> Inputs => inputs;
}

public sealed class Aggregate(Plan input, IReadOnlyList<string> groupBy, IReadOnlyList<Aggregation> aggregates) : UnaryPlan(input)
{
    public IReadOnlyList<string> GroupBy => groupBy;
    public IReadOnlyList<Aggregation> Aggregates => aggregates;
}
