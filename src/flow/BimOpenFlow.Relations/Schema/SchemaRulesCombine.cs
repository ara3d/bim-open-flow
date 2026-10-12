using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>How a join names and checks its output; the defaults are the relation layer's.</summary>
/// <param name="RightSuffix">Appended to a right column whose name the left already has.</param>
/// <param name="DropRightKeys">True when the right key columns are not carried over (table.join).</param>
/// <param name="RequireComparableKeys">True when a key pair must have unifiable types; table.join
/// compares keys as canonical text, so any pair of types will do.</param>
public sealed record JoinOptions(
    string RightSuffix = SchemaInference.RightSuffix,
    bool DropRightKeys = false,
    bool RequireComparableKeys = true);

/// <summary>The rules that combine two or more inputs: join, union, union by name, keyed set operations.</summary>
public static partial class SchemaRules
{
    public static SchemaResult Join(Schema left, Schema right, IReadOnlyList<JoinKey> keys, JoinKind kind, JoinOptions? options = null)
    {
        options ??= new JoinOptions();
        if (RequireAll(left, keys.Select(k => k.Left)) is { } l) return l;
        if (RequireAll(right, keys.Select(k => k.Right)) is { } r) return r;
        if (options.RequireComparableKeys)
            foreach (var key in keys)
            {
                var (a, b) = (left.Find(key.Left)!.Value.Type, right.Find(key.Right)!.Value.Type);
                if (!Comparable(a, b))
                    return SchemaResult.Fail($"Join keys '{key.Left}' ({a}) and '{key.Right}' ({b}) have incompatible types.");
            }
        if (kind is JoinKind.Semi or JoinKind.Anti) return SchemaResult.Of(left);
        var rightKeys = options.DropRightKeys
            ? keys.Select(k => right.Find(k.Right)!.Value.Name).ToHashSet()
            : [];
        var carried = right.Columns.Where(c => !rightKeys.Contains(c.Name)).ToList();
        var names = RightOutputNames(left.Columns.Select(c => c.Name), carried.Select(c => c.Name).ToList(), options.RightSuffix);
        var leftColumns = kind is JoinKind.Right or JoinKind.Full ? left.Columns.Select(Nullable) : left.Columns;
        var rightColumns = kind is JoinKind.Left or JoinKind.Full ? carried.Select(Nullable) : carried;
        return SchemaResult.Of(new Schema([.. leftColumns, .. rightColumns.Select((c, i) => c with { Name = names[i] })]));
    }

    /// <summary>An outer join pads the unmatched side with nulls, so that side's columns are nullable whatever they were.</summary>
    private static Column Nullable(Column column)
        => column with { Nullable = true };

    /// <summary>The names the right side's carried columns take in a join's output. A column the left does
    /// not have keeps its name. A column whose name the left has (ignoring case) gets the suffix, and the
    /// suffix again until the name is free of the left's names, the right's unrenamed names and the
    /// names already given: a = [x, x_b] joined with b = [x] yields x_b_b, never a second x_b. The run
    /// and the rule both call this, so they cannot disagree.</summary>
    public static IReadOnlyList<string> RightOutputNames(IEnumerable<string> leftNames, IReadOnlyList<string> rightNames, string suffix)
    {
        var left = leftNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var taken = left.Concat(rightNames.Where(n => !left.Contains(n))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rightNames.Select(name =>
        {
            if (!left.Contains(name)) return name;
            var renamed = name + suffix;
            while (!taken.Add(renamed)) renamed += suffix;
            return renamed;
        }).ToList();
    }

    /// <summary>Union by position: the inputs must have the same column count and unifiable types.
    /// Names come from the first input; a column that is Integer in one input and Number in another is Number.</summary>
    public static SchemaResult Union(IReadOnlyList<Schema> inputs)
    {
        if (inputs.Count == 0) return SchemaResult.Fail("Union needs at least one input.");
        var first = inputs[0];
        var columns = first.Columns.ToList();
        for (var i = 1; i < inputs.Count; i++)
        {
            if (inputs[i].Columns.Count != first.Columns.Count)
                return SchemaResult.Fail($"Union input {i + 1} has {inputs[i].Columns.Count} columns, expected {first.Columns.Count}.");
            for (var c = 0; c < first.Columns.Count; c++)
            {
                if (!Comparable(columns[c].Type, inputs[i].Columns[c].Type))
                    return SchemaResult.Fail($"Union column '{first.Columns[c].Name}' is {columns[c].Type} in input 1 but {inputs[i].Columns[c].Type} in input {i + 1}.");
                columns[c] = Widen(columns[c], inputs[i].Columns[c]);
            }
        }
        return SchemaResult.Of(new Schema(columns));
    }

    /// <summary>Union by name: a's columns, then the columns only b has. A column in both keeps a's
    /// spelling and takes the wider type; two types that do not unify fail.</summary>
    public static SchemaResult UnionByName(Schema a, Schema b)
    {
        var columns = new List<Column>();
        foreach (var column in a.Columns)
        {
            if (b.Find(column.Name) is not { } other)
            {
                columns.Add(column with { Nullable = true });
                continue;
            }
            if (!Comparable(column.Type, other.Type))
                return SchemaResult.Fail($"Column '{column.Name}' is {column.Type} in the first input but {other.Type} in the second.");
            columns.Add(Widen(column, other));
        }
        columns.AddRange(b.Columns.Where(c => !a.Has(c.Name)).Select(c => c with { Nullable = true }));
        return SchemaResult.Of(new Schema(columns));
    }

    /// <summary>The names in a that b lacks, and in b that a lacks, compared ignoring case.</summary>
    public static (IReadOnlyList<string> MissingFromB, IReadOnlyList<string> ExtraInB) ColumnSetDifference(
        IEnumerable<string> a, IEnumerable<string> b)
    {
        var (aNames, bNames) = (a.ToList(), b.ToList());
        return (aNames.Except(bNames, StringComparer.OrdinalIgnoreCase).ToList(),
            bNames.Except(aNames, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>A key-set operation (table.setOp): a's rows are kept or extended by key, so a's shape
    /// passes through. The key must exist in both inputs; a union also needs the same column set.</summary>
    public static SchemaResult KeyedSetOp(Schema a, Schema b, string key, bool requireSameColumns)
    {
        if (RequireAll(a, [key]) is { } inA) return inA;
        if (RequireAll(b, [key]) is { } inB) return inB;
        if (!requireSameColumns) return SchemaResult.Of(a);
        var (missing, extra) = ColumnSetDifference(a.Columns.Select(c => c.Name), b.Columns.Select(c => c.Name));
        return missing.Count > 0 || extra.Count > 0
            ? SchemaResult.Fail(SetOpColumnMessage(missing, extra))
            : SchemaResult.Of(a);
    }

    /// <summary>The wording for a union whose inputs disagree on columns; the node's evaluation uses it too.</summary>
    public static string SetOpColumnMessage(IReadOnlyList<string> missingFromB, IReadOnlyList<string> extraInB)
        => $"union requires b to share a's columns; missing from b: [{string.Join(", ", missingFromB)}], extra in b: [{string.Join(", ", extraInB)}].";

    internal static bool Comparable(ColumnType a, ColumnType b)
        => a == b || TypeChecker.Unify(a.ToScalarType(), b.ToScalarType()).Ok && a.ToScalarType() is not null && b.ToScalarType() is not null;

    /// <summary>The column both sides unify to: Integer with Number is Number, otherwise the first's type.
    /// Nullable if either is.</summary>
    private static Column Widen(Column a, Column b)
        => a with
        {
            Type = a.Type is ColumnType.Integer or ColumnType.Number && b.Type is ColumnType.Integer or ColumnType.Number
                ? (a.Type == b.Type ? a.Type : ColumnType.Number)
                : a.Type,
            Nullable = a.Nullable || b.Nullable,
        };
}
