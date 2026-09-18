using System.Globalization;
using Ara3D.DataFlowEngine.Expressions.Parsing;

namespace BimOpenFlow.Relations;

/// <summary>Renders an expression AST back into the expression language, fully
/// parenthesized and with every identifier bracket-quoted, so the text is canonical
/// and re-parses to an equal tree.</summary>
public static class ExprText
{
    public static string Render(this Expr expr)
        => expr switch
        {
            BooleanLiteral b => b.Value ? "true" : "false",
            IntegerLiteral i => i.Value.ToString(CultureInfo.InvariantCulture),
            NumberLiteral n => n.Value.ToString("R", CultureInfo.InvariantCulture),
            TextLiteral t => QuoteText(t.Value),
            NullLiteral => "null",
            Identifier id => QuoteIdentifier(id.Name),
            Unary u => $"({u.Op.Text()} {u.Operand.Render()})",
            Binary b => $"({b.Left.Render()} {b.Op.Text()} {b.Right.Render()})",
            Conditional c => $"({c.Condition.Render()} ? {c.WhenTrue.Render()} : {c.WhenFalse.Render()})",
            Call c => $"{c.Name}({string.Join(", ", c.Args.Select(Render))})",
            _ => throw new ArgumentException($"Unknown expression node {expr.GetType().Name}", nameof(expr)),
        };

    public static string QuoteText(string value)
        => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\t", "\\t") + "'";

    public static string QuoteIdentifier(string name)
        => "[" + name.Replace("]", "]]") + "]";
}
