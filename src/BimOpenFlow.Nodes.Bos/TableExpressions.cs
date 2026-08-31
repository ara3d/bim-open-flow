using System.Globalization;
using Ara3D.DataFlowEngine.Expressions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Bos;

/// <summary>Bridges IDataTable columns to the expression facade: the column environment,
/// compile-or-throw, and cell/scalar conversion. Columns whose .NET type has no scalar
/// mapping are simply absent from the environment (unavailable to expressions).</summary>
public static class TableExpressions
{
    public readonly record struct ColumnBinding(int Index, ScalarType Type);

    public static ScalarType? ToScalarType(Type type)
        => type == typeof(bool) ? ScalarType.Boolean
            : type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort)
              || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
                ? ScalarType.Integer
            : type == typeof(float) || type == typeof(double) || type == typeof(decimal) ? ScalarType.Number
            : type == typeof(string) ? ScalarType.Text
            : null;

    public static Type ToNetType(this ScalarType type)
        => type switch
        {
            ScalarType.Boolean => typeof(bool),
            ScalarType.Integer => typeof(long),
            ScalarType.Number => typeof(double),
            _ => typeof(string),
        };

    /// <summary>Scalar-typed columns by name; on duplicate names the first column wins.</summary>
    public static IReadOnlyDictionary<string, ColumnBinding> Bindings(this IDataTable table)
    {
        var r = new Dictionary<string, ColumnBinding>();
        foreach (var c in table.Columns)
            if (ToScalarType(c.Descriptor.Type) is { } t)
                r.TryAdd(c.Descriptor.Name, new ColumnBinding(c.ColumnIndex, t));
        return r;
    }

    /// <summary>Parses and type-checks, throwing ArgumentException listing every error with its offset.</summary>
    public static CheckedExpression Compile(string kind, string paramName, string text,
        IReadOnlyDictionary<string, ColumnBinding> bindings)
    {
        var env = bindings.ToDictionary(kv => kv.Key, kv => kv.Value.Type);
        var result = Expression.Parse(text).Check(env);
        return result.Success
            ? result
            : throw new ArgumentException(
                $"{kind}: invalid expression in '{paramName}': {string.Join("; ", result.Errors)}");
    }

    public static Scalar? CellToScalar(object? value, ScalarType type)
        => value is null or DBNull ? null : type switch
        {
            ScalarType.Boolean => new BooleanScalar(Convert.ToBoolean(value, CultureInfo.InvariantCulture)),
            ScalarType.Integer => new IntegerScalar(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            ScalarType.Number => new NumberScalar(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            _ => new TextScalar(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
        };

    public static object? ToCell(this Scalar? scalar)
        => scalar switch
        {
            null => null,
            BooleanScalar b => b.Value,
            IntegerScalar i => i.Value,
            NumberScalar n => n.Value,
            TextScalar t => t.Value,
            _ => throw new ArgumentException($"Unknown scalar {scalar.GetType().Name}", nameof(scalar)),
        };

    public static Func<string, Scalar?> RowLookup(this IDataTable table,
        IReadOnlyDictionary<string, ColumnBinding> bindings, int row)
        => name => bindings.TryGetValue(name, out var b) ? CellToScalar(table[b.Index, row], b.Type) : null;
}
