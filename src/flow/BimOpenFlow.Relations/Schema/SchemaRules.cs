using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataFlowEngine.Expressions.Parsing;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>The one rule set for the shape of a table operator's output: functions over the input
/// schemas and the operator's arguments. The relation layer calls them from <see cref="SchemaInference"/>
/// with arguments taken from plan operators; the table.* nodes call them with arguments parsed from
/// their parameters. Errors are values, and a missing column fails naming it (and the nearest column,
/// see <see cref="NoColumnMessage"/>). Column names resolve case-insensitively, as <see cref="Schema.Find"/>
/// does; the stored spelling flows downstream. An operator whose run warns and skips an absent column
/// passes skipMissing, and gets the schema of the run plus the warnings.</summary>
public static partial class SchemaRules
{
    /// <param name="skipMissing">True when the operator's run skips a term whose column is absent and
    /// warns (table.project); false when an absent column fails.</param>
    public static SchemaResult Select(Schema input, IReadOnlyList<Projection> terms, bool skipMissing = false)
    {
        if (!skipMissing && RequireAll(input, terms.Select(c => c.Column)) is { } missing) return missing;
        var (present, warnings) = SplitMissing(input, terms, t => t.Column);
        var columns = present.Select(c => Aliased(input.Find(c.Column)!.Value, c.Alias)).ToList();
        return Projection.FirstDuplicate(columns.Select(c => c.Name)) is { } clash
            ? SchemaResult.Fail($"Select produces duplicate column '{clash}'.")
            : SchemaResult.Of(new Schema(columns), warnings);
    }

    /// <summary>The complement of <see cref="Select"/>: every column except the named ones. Dropping all is an error.</summary>
    /// <param name="skipMissing">True when the run skips an absent name and warns (table.drop).</param>
    public static SchemaResult Drop(Schema input, IEnumerable<string> names, bool skipMissing = false)
    {
        var requested = names.ToList();
        if (!skipMissing && RequireAll(input, requested) is { } missing) return missing;
        var (dropped, warnings) = SplitMissing(input, requested, n => n);
        var kept = input.Columns.Where(c => !dropped.Contains(c.Name, StringComparer.OrdinalIgnoreCase)).ToList();
        return kept.Count == 0
            ? SchemaResult.Fail("Dropping every column leaves an empty table.")
            : SchemaResult.Of(new Schema(kept), warnings);
    }

    /// <param name="skipMissing">True when the run skips a renaming whose source is absent and warns (table.rename).</param>
    public static SchemaResult Rename(Schema input, IReadOnlyList<Renaming> renamings, bool skipMissing = false)
    {
        if (!skipMissing && RequireAll(input, renamings.Select(x => x.From)) is { } missing) return missing;
        var (present, warnings) = SplitMissing(input, renamings, x => x.From);
        var map = new Dictionary<string, string>();
        foreach (var r in present)
            if (!map.TryAdd(input.Find(r.From)!.Value.Name, r.To))
                return SchemaResult.Fail($"Column '{r.From}' is renamed more than once.");
        var renamed = input.Map(c => map.TryGetValue(c.Name, out var to) ? c with { Name = to } : c);
        return Projection.FirstDuplicate(renamed.Columns.Select(c => c.Name)) is { } clash
            ? SchemaResult.Fail($"Rename produces duplicate column '{clash}'.")
            : SchemaResult.Of(renamed, warnings);
    }

    /// <summary>Casts one column in place, or appends the cast as a new column when newName is given.
    /// The type is the type the output column will have, which a node may choose (table.cast hands
    /// dates back as ISO text).</summary>
    public static SchemaResult Cast(Schema input, string column, ColumnType type, string? newName = null)
    {
        if (input.Find(column) is not { } source) return NoColumn(input, column);
        if (newName is null)
            return SchemaResult.Of(input.Map(c => c.Name == source.Name ? c with { Type = type } : c));
        return input.Has(newName)
            ? SchemaResult.Fail($"Column '{newName}' already exists.")
            : SchemaResult.Of(input.Append(new Column(newName, type)));
    }

    /// <summary>The message of a derive whose expression has no type because it is always null.</summary>
    public const string AlwaysNullMessage = "'expr' is always null; its column type cannot be inferred.";

    /// <param name="nullIsError">True when the run refuses an always-null expression (table.derive); the
    /// relation layer accepts it and types the column Unknown.</param>
    public static SchemaResult Derive(Schema input, string name, Expr expression, bool nullIsError = false)
    {
        if (input.Has(name)) return SchemaResult.Fail($"Column '{name}' already exists.");
        var (type, errors) = expression.TypeOf(input);
        if (errors.Count == 0 && type is null && nullIsError) return SchemaResult.Fail(AlwaysNullMessage);
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

    internal static SchemaResult NoColumn(Schema schema, string column)
        => SchemaResult.Fail(NoColumnMessage(schema, column));

    /// <summary>Null when every column exists, else the failure naming the first missing one.</summary>
    internal static SchemaResult? RequireAll(Schema schema, IEnumerable<string> columns)
        => columns.FirstOrDefault(c => !schema.Has(c)) is { } missing ? NoColumn(schema, missing) : null;
}
