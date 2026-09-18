namespace BimOpenFlow.Relations.Tests;

public class ExprSqlTests
{
    [TestCase("a + 1", "(\"a\" + 1)")]
    [TestCase("a / b", "(CAST(\"a\" AS DOUBLE) / \"b\")")]
    [TestCase("a & 'x'", "(\"a\" || 'x')")]
    [TestCase("a != null", "(\"a\" <> NULL)")]
    [TestCase("not a or b == 2.5", "((NOT \"a\") OR (\"b\" = 2.5))")]
    [TestCase("x ? 1 : 2", "(CASE WHEN \"x\" THEN 1 ELSE 2 END)")]
    [TestCase("max(a, 3) + len(lower([Fire Rating]))", "(greatest(\"a\", 3) + length(lower(\"Fire Rating\")))")]
    [TestCase("startswith(n, 'it\\'s')", "starts_with(\"n\", 'it''s')")]
    [TestCase("2.0", "2.0")]
    [TestCase("-1", "(-1)")]
    public void EmitsDuckDbSql(string text, string expected)
        => Assert.That(text.ParseExpr().ToSql(), Is.EqualTo(expected));

    [Test]
    public void UnknownFunctionThrows()
        => Assert.Throws<ArgumentException>(() => "nope(1)".ParseExpr().ToSql());
}
