using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Parsing;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>The one rule set for the shape of a table operator's output: functions over the input
/// schemas and the operator's arguments. The relation layer calls them from <see cref="SchemaInference"/>
/// with arguments taken from plan operators; the table.* nodes call them with arguments parsed from
/// their parameters. Errors are values, and a missing column fails naming it. Column names resolve
/// case-insensitively, as <see cref="Schema.Find"/> does; the stored spelling flows downstream.</summary>
public static partial class SchemaRules
{
    public static SchemaResult Select(Schema input, IReadOnlyList<Projection> terms)
    {
        if (RequireAll(input, terms.Select(c => c.Column)) is { } missing) return missing;
        var columns = terms.Select(c => Aliased(input.Find(c.Column)!.Value, c.Alias)).ToList();
        return Projection.FirstDuplicate(columns.Select(c => c.Name)) is { } clash
            ? SchemaResult.Fail($"Select produces duplicate column '{clash}'.")
            : SchemaResult.Of(new Schema(columns));
    }

    /// <summary>The complement of <see cref="Select"/>: every column except the named ones. Dropping all is an error.</summary>
    public static SchemaResult Drop(Schema input, IEnumerable<string> names)
    {
        var dropped = names.ToList();
        if (RequireAll(input, dropped) is { } missing) return missing;
        var kept = input.Columns.Where(c => !dropped.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        return kept.Count == 0 ? SchemaResult.Fail("Dropping every column leaves an empty table.") : SchemaResult.Of(new Schema(kept));
    }

    public static SchemaResult Rename(Schema input, IReadOnlyList<Renaming> renamings)
    {
        if (RequireAll(input, renamings.Select(x => x.From)) is { } missing) return missing;
        var map = new Dictionary<string, string>();
        foreach (var r in renamings)
            if (!map.TryAdd(input.Find(r.From)!.Value.Name, r.To))
                return SchemaResult.Fail($"Column '{r.From}' is renamed more than once.");
        var renamed = input.Map(c => map.TryGetValue(c.Name, out var to) ? c with { Name = to } : c);
        return Projection.FirstDuplicate(renamed.Columns.Select(c => c.Name)) is { } clash
            ? SchemaResult.Fail($"Rename produces duplicate column '{clash}'.")
            : SchemaResult.Of(renamed);
    }

    /// <summary>Casts one column in place, or appends the cast as a new column when newName is given.
    /// The type is the type the output column will have, which a node may choose (table.cast hands
    /// dates back as ISO text).</summary>
    public static SchemaResult Cast(Schema input, string column, ColumnType type, string? newName = null)
    {
        if (input.Find(column) is not { } source) return NoColumn(column);
        if (newName is null)
            return SchemaResult.Of(input.Map(c => c.Name == source.Name ? c with { Type = type } : c));
        return input.Has(newName)
            ? SchemaResult.Fail($"Column '{newName}' already exists.")
            : SchemaResult.Of(input.Append(new Column(newName, type)));
    }

    public static SchemaResult Derive(Schema input, string name, Expr expression)
    {
        if (input.Has(name)) return SchemaResult.Fail($"Column '{name}' already exists.");
        var (type, errors) = expression.TypeOf(input);
        return errors.Count > 0
            ? SchemaResult.Fail(errors.Select(e => new SchemaError($"In '{name}': {e}")).ToList())
            : SchemaResult.Of(input.Append(new Column(name, type.ToColumnType())));
    }

    public static SchemaResult Filter(Schema input, Expr predicate)
    {
        var (type, errors) = predicate.TypeOf(input);
        return errors.Count > 0 ? SchemaResult.Fail(errors.Select(e => new SchemaError($"In predicate: {e}")).ToList())
            : type is not (null or ScalarType.Boolean) ? SchemaResult.Fail($"Predicate must be Boolean, but it is {type}.")
            : SchemaResult.Of(input);
    }

    /// <summary>Sorting keeps the shape; the keys must exist.</summary>
    public static SchemaResult Sort(Schema input, IEnumerable<string> keys)
        => RequireAll(input, keys) ?? SchemaResult.Of(input);

    /// <summary>Distinct keeps the shape; the key columns (none means the whole row) must exist.</summary>
    public static SchemaResult Distinct(Schema input, IEnumerable<string> keys)
        => RequireAll(input, keys) ?? SchemaResult.Of(input);

    private static Column Aliased(Column column, string? alias)
        => alias is null ? column : column with { Name = alias };

    internal static SchemaResult NoColumn(string column)
        => SchemaResult.Fail($"No column named '{column}'.");

    /// <summary>Null when every column exists, else the failure naming the first missing one.</summary>
    internal static SchemaResult? RequireAll(Schema schema, IEnumerable<string> columns)
        => columns.FirstOrDefault(c => !schema.Has(c)) is { } missing ? NoColumn(missing) : null;
}
