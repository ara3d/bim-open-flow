using System.Text.RegularExpressions;

namespace BimOpenFlow.Nodes.Relations;

/// <summary>Reading plans off input ports and parsing the pack's small parameter grammars.</summary>
public static partial class RelationArgs
{
    public static Plan PlanInput(this IReadOnlyList<FlowValue> inputs, int index, string kind)
        => index < inputs.Count && inputs[index] is RelationValue { Payload: Plan plan }
            ? plan
            : throw new ArgumentException($"{kind}: input {index} must be a Relation with an in-process plan.");

    public static Plan? OptionalPlanInput(this IReadOnlyList<FlowValue> inputs, int index, string kind)
        => index < inputs.Count && inputs[index] is RelationValue ? inputs.PlanInput(index, kind) : null;

    /// <summary>"col, col2 desc, col3 asc" to sort keys.</summary>
    public static IReadOnlyList<SortKey> ParseSortKeys(string text, string kind)
        => text.SplitNames().Select(term =>
        {
            var tokens = term.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length switch
            {
                1 => new SortKey(tokens[0]),
                2 when tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase) => new SortKey(tokens[0], true),
                2 when tokens[1].Equals("asc", StringComparison.OrdinalIgnoreCase) => new SortKey(tokens[0]),
                _ => throw new ArgumentException($"{kind}: cannot parse sort term '{term}'."),
            };
        }).ToList();

    [GeneratedRegex(@"^(?<func>[a-zA-Z]+)\s*\(\s*(?<col>\*|[^)]*?)\s*\)\s+as\s+(?<name>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AggregateSpec();

    /// <summary>"count(*) as n, sum(height) as total" to aggregations.</summary>
    public static IReadOnlyList<Aggregation> ParseAggregations(string text, string kind)
        => text.SplitNames().Select(spec =>
        {
            var match = AggregateSpec().Match(spec);
            if (!match.Success)
                throw new ArgumentException($"{kind}: aggregate '{spec}' must look like 'func(column) as name'.");
            var function = match.Groups["func"].Value.ToLowerInvariant() switch
            {
                "count" => AggregateFunction.Count,
                "sum" => AggregateFunction.Sum,
                "min" => AggregateFunction.Min,
                "max" => AggregateFunction.Max,
                "avg" => AggregateFunction.Avg,
                var f => throw new ArgumentException($"{kind}: unknown aggregate function '{f}' in '{spec}'."),
            };
            var column = match.Groups["col"].Value;
            if (column == "*" && function != AggregateFunction.Count)
                throw new ArgumentException($"{kind}: only count may aggregate over '*' ('{spec}').");
            return new Aggregation(function, column == "*" ? null : column, match.Groups["name"].Value.Trim());
        }).ToList();

    public static JoinKind ParseJoinKind(string text, string kind)
        => Enum.TryParse<JoinKind>(text, ignoreCase: true, out var result)
            ? result
            : throw new ArgumentException($"{kind}: unknown join kind '{text}'.");
}
