using Ara3D.BimOpenSchema;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Support;

/// <summary>One column of a declared schema: a registry kind (its name, its scalar type) or, for a
/// column that is no column kind, a plain name and type. Nullable defaults to true, the honest
/// answer unless the producing code never emits a null.</summary>
public readonly record struct KindColumn(string Name, ColumnType Type, string? Kind = null, bool Nullable = true)
{
    public static implicit operator KindColumn(ColumnKind kind)
        => kind.Declared();

    /// <summary>A column that is no registered kind (a one-off summary column).</summary>
    public static KindColumn Plain(string name, ColumnType type, bool nullable = true)
        => new(name, type, null, nullable);
}

/// <summary>Turns column kinds into engine schemas. The mapping from the registry's
/// <see cref="ColumnScalarType"/> to the engine's <see cref="ColumnType"/> lives here and nowhere else.
/// Pass kinds as the static members of <see cref="ColumnKinds"/>, not through <c>Find</c>, which
/// ignores case and would give the chart tables' numeric <c>value</c> the Text kind <c>Value</c>.</summary>
public static class KindSchemas
{
    public static ColumnType ToColumnType(this ColumnScalarType type)
        => type switch
        {
            ColumnScalarType.Boolean => ColumnType.Boolean,
            ColumnScalarType.Integer => ColumnType.Integer,
            ColumnScalarType.Number => ColumnType.Number,
            ColumnScalarType.Text => ColumnType.Text,
            ColumnScalarType.Date => ColumnType.Date,
            ColumnScalarType.Timestamp => ColumnType.Timestamp,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No engine column type for this scalar type."),
        };

    /// <summary>The kind as a declared column; nullable unless <paramref name="nullable"/> is false.</summary>
    public static KindColumn Declared(this ColumnKind kind, bool nullable = true)
        => new(kind.Name, kind.Type.ToColumnType(), kind.Name, nullable);

    /// <summary>The kind as a column that never holds null.</summary>
    public static KindColumn NotNull(this ColumnKind kind)
        => kind.Declared(nullable: false);

    public static Column ToColumn(this KindColumn column)
        => new(column.Name, column.Type, column.Nullable);

    public static Schema ToSchema(this IEnumerable<KindColumn> columns)
        => new([.. columns.Select(ToColumn)]);

    /// <summary>A Static spec for one table output, with the column-to-kind map of every column that
    /// names a kind.</summary>
    public static SchemaSpec Static(string output, params KindColumn[] columns)
        => SchemaSpec.Static(output, columns.ToSchema(),
            columns.Where(c => c.Kind is not null).ToDictionary(c => c.Name, c => c.Kind!));

    /// <summary>The input's columns followed by the given ones, or the input's failure.</summary>
    public static SchemaResult Append(this SchemaResult input, params KindColumn[] columns)
        => input.Ok
            ? SchemaResult.Of(new Schema([.. input.Schema!.Columns, .. columns.Select(ToColumn)]))
            : input;
}
