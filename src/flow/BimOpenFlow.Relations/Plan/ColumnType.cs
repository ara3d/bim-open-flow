using Ara3D.DataFlowEngine.Expressions;

namespace BimOpenFlow.Relations;

/// <summary>The closed column type vocabulary. The first four are the expression
/// language's scalar types; the rest can be read and passed through but not computed on.</summary>
public enum ColumnType
{
    Boolean,
    Integer,
    Number,
    Text,
    Date,
    Timestamp,
    Binary,
    Unknown,
}

public static class ColumnTypes
{
    public static ScalarType? ToScalarType(this ColumnType type)
        => type switch
        {
            ColumnType.Boolean => ScalarType.Boolean,
            ColumnType.Integer => ScalarType.Integer,
            ColumnType.Number => ScalarType.Number,
            ColumnType.Text => ScalarType.Text,
            _ => null,
        };

    public static ColumnType ToColumnType(this ScalarType? type)
        => type switch
        {
            ScalarType.Boolean => ColumnType.Boolean,
            ScalarType.Integer => ColumnType.Integer,
            ScalarType.Number => ColumnType.Number,
            ScalarType.Text => ColumnType.Text,
            _ => ColumnType.Unknown,
        };
}
