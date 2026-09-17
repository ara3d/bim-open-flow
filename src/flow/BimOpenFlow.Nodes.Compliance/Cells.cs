using System.Globalization;
using Ara3D.DataFlowEngine.Expressions;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>Maps table cells and column CLR types to expression scalars.</summary>
internal static class Cells
{
    /// <summary>The expression scalar type for a column CLR type; null when not addressable from expressions.</summary>
    public static ScalarType? ToScalarType(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(bool))
            return ScalarType.Boolean;
        if (t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long))
            return ScalarType.Integer;
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            return ScalarType.Number;
        if (t == typeof(string))
            return ScalarType.Text;
        return null;
    }

    /// <summary>Null (and any unmapped cell kind) becomes the null scalar, which propagates.</summary>
    public static Scalar? ToScalar(object? value)
        => value switch
        {
            null => null,
            bool b => new BooleanScalar(b),
            sbyte or byte or short or ushort or int or uint or long
                => new IntegerScalar(Convert.ToInt64(value, CultureInfo.InvariantCulture)),
            float or double or decimal
                => new NumberScalar(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
            string s => new TextScalar(s),
            _ => null,
        };
}
