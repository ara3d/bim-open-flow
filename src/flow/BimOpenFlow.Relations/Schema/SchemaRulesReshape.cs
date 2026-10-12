using System.Globalization;

namespace BimOpenFlow.Relations;

/// <summary>The window functions of table.window.</summary>
public enum WindowFunction { RowNumber, Rank, DenseRank, Lag, Lead, CumSum, MovingAvg, PercentOfTotal }

/// <summary>Rules for the table.* and cleaning nodes that have no plan operator: they add, split or fold
/// columns, or change which cells are null.</summary>
public static partial class SchemaRules
{
    /// <summary>Appends one window column. Rankings are Integer and never null; lag and lead take the source
    /// column's type; a cumulative sum keeps Integer or Number; a moving average and a percent of total are
    /// Number. Sums and averages need a numeric source column. Partition and order columns must exist.</summary>
    public static SchemaResult Window(Schema input, string name, WindowFunction function, string? column,
        IEnumerable<string> partitionBy, IEnumerable<string> orderBy)
    {
        if (input.Has(name)) return SchemaResult.Fail($"Column '{name}' already exists.");
        var ranking = function is WindowFunction.RowNumber or WindowFunction.Rank or WindowFunction.DenseRank;
        if (!ranking && column is null) return SchemaResult.Fail($"{function} needs a source column.");
        if (!ranking && RequireAll(input, [column!]) is { } noSource) return noSource;
        if (RequireAll(input, partitionBy.Concat(orderBy)) is { } missing) return missing;
        if (ranking) return SchemaResult.Of(input.Append(new Column(name, ColumnType.Integer, Nullable: false)));

        var source = input.Find(column!)!.Value;
        var numeric = source.Type is ColumnType.Integer or ColumnType.Number;
        if (function is not (WindowFunction.Lag or WindowFunction.Lead) && !numeric)
            return SchemaResult.Fail($"Cannot apply {function} to '{source.Name}' of type {source.Type}.");
        var type = function switch
        {
            WindowFunction.Lag or WindowFunction.Lead or WindowFunction.CumSum => source.Type,
            _ => ColumnType.Number,
        };
        return SchemaResult.Of(input.Append(new Column(name, type)));
    }

    /// <summary>Splits a text column into one new Text column per name. The source column stays when keep is
    /// true. A new name that the input already has, or that appears twice, fails.</summary>
    public static SchemaResult SplitColumn(Schema input, string column, IReadOnlyList<string> names, bool keep)
    {
        if (input.Find(column) is not { } source) return NoColumn(input, column);
        if (source.Type != ColumnType.Text)
            return SchemaResult.Fail($"Column '{source.Name}' is {source.Type}, not Text.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            if (input.Has(name)) return SchemaResult.Fail($"New column '{name}' already exists.");
            if (!seen.Add(name)) return SchemaResult.Fail($"New column '{name}' is named more than once.");
        }
        var kept = keep ? input.Columns : input.Columns.Where(c => c.Name != source.Name).ToList();
        return SchemaResult.Of(new Schema([.. kept, .. names.Select(n => new Column(n, ColumnType.Text))]));
    }

    /// <summary>Wide to long. The result is the kept columns, then the name column (Text) and the value
    /// column. Empty columns means every column that is not kept. When the unpivoted columns differ in kind
    /// (Boolean, Integer, Number, anything else as Text) the value column is Text, with a warning. Columns
    /// that are neither kept nor unpivoted are dropped, with a warning.</summary>
    public static SchemaResult Unpivot(Schema input, IReadOnlyList<string> keep, IReadOnlyList<string> columns,
        string nameColumn, string valueColumn)
    {
        if (RequireAll(input, keep.Concat(columns)) is { } missing) return missing;
        var kept = keep.Select(k => input.Find(k)!.Value).ToList();
        var folded = (columns.Count > 0
            ? columns.Select(c => input.Find(c)!.Value)
            : input.Columns.Where(c => !keep.Contains(c.Name, StringComparer.OrdinalIgnoreCase))).ToList();
        if (folded.Count == 0) return SchemaResult.Fail("No columns to unpivot.");
        foreach (var output in new[] { nameColumn, valueColumn })
            if (kept.Any(k => string.Equals(k.Name, output, StringComparison.OrdinalIgnoreCase)))
                return SchemaResult.Fail($"Output column '{output}' collides with a kept column.");
        if (string.Equals(nameColumn, valueColumn, StringComparison.OrdinalIgnoreCase))
            return SchemaResult.Fail($"The name and value columns are both called '{nameColumn}'.");

        var warnings = new List<SchemaError>();
        if (columns.Count > 0)
        {
            var dropped = input.Columns.Where(c => !kept.Any(k => k.Name == c.Name) && !folded.Any(f => f.Name == c.Name)).ToList();
            if (dropped.Count > 0)
                warnings.Add(new SchemaError($"Columns not kept or unpivoted are dropped: {string.Join(", ", dropped.Select(c => c.Name))}."));
        }
        var kinds = folded.Select(c => c.Type is ColumnType.Boolean or ColumnType.Integer or ColumnType.Number ? c.Type : ColumnType.Text)
            .Distinct().ToList();
        if (kinds.Count > 1)
            warnings.Add(new SchemaError("Columns have mixed types; values widened to text."));
        var valueType = kinds.Count == 1 ? kinds[0] : ColumnType.Text;
        return SchemaResult.Of(new Schema([.. kept, new Column(nameColumn, ColumnType.Text, Nullable: false),
            new Column(valueColumn, valueType, Nullable: false)]), warnings);
    }

    /// <summary>Rows with a null in any (or, with all, every) listed column are dropped, so the listed
    /// columns are never null afterwards; with all and several columns a row may keep a null. No names
    /// means every column.</summary>
    public static SchemaResult DropNulls(Schema input, IReadOnlyList<string> columns, bool any)
    {
        if (RequireAll(input, columns) is { } missing) return missing;
        var listed = columns.Count == 0 ? input.Columns.Select(c => c.Name).ToList() : columns.Select(c => input.Find(c)!.Value.Name).ToList();
        var notNull = any || listed.Distinct(StringComparer.Ordinal).Count() == 1;
        return SchemaResult.Of(notNull ? input.Map(c => listed.Contains(c.Name) ? c with { Nullable = false } : c) : input);
    }

    /// <summary>Fills nulls in the listed columns, which keep their types. A constant fill needs a Text,
    /// Integer, Number or Boolean column and a value that parses as its type, and leaves the column never
    /// null; forward and backward fills can leave nulls at the ends.</summary>
    public static SchemaResult FillNulls(Schema input, IReadOnlyList<string> columns, bool constant, string value, IEnumerable<string> partitionBy)
    {
        if (RequireAll(input, columns.Concat(partitionBy)) is { } missing) return missing;
        var targets = columns.Select(c => input.Find(c)!.Value).ToList();
        if (constant)
            foreach (var target in targets)
                if (ConstantFillError(target, value) is { } error) return SchemaResult.Fail(error);
        var names = targets.Select(t => t.Name).ToHashSet();
        return SchemaResult.Of(input.Map(c => constant && names.Contains(c.Name) ? c with { Nullable = false } : c));
    }

    private static string? ConstantFillError(Column column, string value)
    {
        var cast = $"Value '{value}' cannot be cast to the type of column '{column.Name}'.";
        return column.Type switch
        {
            ColumnType.Text => null,
            ColumnType.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : cast,
            ColumnType.Number => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? null : cast,
            ColumnType.Boolean => bool.TryParse(value, out _) ? null : cast,
            _ => $"Column '{column.Name}' has unsupported type '{column.Type}' for a constant fill.",
        };
    }

    /// <summary>The shape stays; every named column must be Text.</summary>
    public static SchemaResult TextInPlace(Schema input, IEnumerable<string> columns)
    {
        if (RequireAll(input, columns) is { } missing) return missing;
        foreach (var column in columns.Select(c => input.Find(c)!.Value))
            if (column.Type != ColumnType.Text)
                return SchemaResult.Fail($"Column '{column.Name}' is {column.Type}, not a text column.");
        return SchemaResult.Of(input);
    }

    /// <summary>The names of the Text columns, for a node whose empty list means every text column.</summary>
    public static IReadOnlyList<string> TextColumnNames(Schema input)
        => input.Columns.Where(c => c.Type == ColumnType.Text).Select(c => c.Name).ToList();

    /// <summary>Appends a nullable Text column made from a Text column (text.extract).</summary>
    public static SchemaResult AppendTextFrom(Schema input, string fromColumn, string name)
    {
        if (TextInPlace(input, [fromColumn]) is { Ok: false } bad) return bad;
        return input.Has(name) ? SchemaResult.Fail($"Column '{name}' already exists.") : SchemaResult.Of(input.Append(new Column(name, ColumnType.Text)));
    }
}
