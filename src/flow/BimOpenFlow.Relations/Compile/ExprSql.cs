using System.Globalization;
using Ara3D.DataFlowEngine.Expressions.Parsing;
using Ara3D.DataFlowEngine.Expressions.Typing;

namespace BimOpenFlow.Relations;

/// <summary>Emits an expression as DuckDB SQL, fully parenthesized. Division is always
/// floating point and the text operator is ||, matching the language's static semantics.</summary>
public static class ExprSql
{
    public static string ToSql(this Expr expr)
        => expr switch
        {
            BooleanLiteral b => b.Value ? "TRUE" : "FALSE",
            IntegerLiteral i => i.Value.ToString(CultureInfo.InvariantCulture),
            NumberLiteral n => Number(n.Value),
            TextLiteral t => t.Value.Literal(),
            NullLiteral => "NULL",
            Identifier id => id.Name.Ident(),
            Unary { Op: UnaryOp.Negate } u => $"(-{u.Operand.ToSql()})",
            Unary u => $"(NOT {u.Operand.ToSql()})",
            Binary { Op: BinaryOp.Div } b => $"(CAST({b.Left.ToSql()} AS DOUBLE) / {b.Right.ToSql()})",
            Binary b => $"({b.Left.ToSql()} {Operator(b.Op)} {b.Right.ToSql()})",
            Conditional c => $"(CASE WHEN {c.Condition.ToSql()} THEN {c.WhenTrue.ToSql()} ELSE {c.WhenFalse.ToSql()} END)",
            Call c => $"{Function(c.Name)}({string.Join(", ", c.Args.Select(ToSql))})",
            _ => throw new ArgumentException($"Unknown expression node {expr.GetType().Name}", nameof(expr)),
        };

    private static string Number(double value)
        => double.IsNaN(value) ? "'NaN'::DOUBLE"
            : double.IsPositiveInfinity(value) ? "'Infinity'::DOUBLE"
            : double.IsNegativeInfinity(value) ? "'-Infinity'::DOUBLE"
            : value.ToString("R", CultureInfo.InvariantCulture) is var text && text.Contains('.') || text.Contains('E') ? text
            : text + ".0";

    private static string Operator(BinaryOp op)
        => op switch
        {
            BinaryOp.Mul => "*",
            BinaryOp.Mod => "%",
            BinaryOp.Add => "+",
            BinaryOp.Sub => "-",
            BinaryOp.Concat => "||",
            BinaryOp.Eq => "=",
            BinaryOp.Ne => "<>",
            BinaryOp.Lt => "<",
            BinaryOp.Le => "<=",
            BinaryOp.Gt => ">",
            BinaryOp.Ge => ">=",
            BinaryOp.And => "AND",
            BinaryOp.Or => "OR",
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };

    private static string Function(string name)
        => Builtins.FromName(name) switch
        {
            Builtin.Abs => "abs",
            Builtin.Min => "least",
            Builtin.Max => "greatest",
            Builtin.Round => "round",
            Builtin.Floor => "floor",
            Builtin.Ceil => "ceil",
            Builtin.Len => "length",
            Builtin.Lower => "lower",
            Builtin.Upper => "upper",
            Builtin.Contains => "contains",
            Builtin.StartsWith => "starts_with",
            Builtin.EndsWith => "ends_with",
            Builtin.Coalesce => "coalesce",
            _ => throw new ArgumentException($"Unknown function '{name}'.", nameof(name)),
        };
}
