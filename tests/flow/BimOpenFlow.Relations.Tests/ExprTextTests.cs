namespace BimOpenFlow.Relations.Tests;

/// <summary>Rendering an expression must re-parse to the same canonical text.</summary>
public class ExprTextTests
{
    [TestCase("a + 1", "([a] + 1)")]
    [TestCase("a + b * c", "([a] + ([b] * [c]))")]
    [TestCase("(a + b) * c", "(([a] + [b]) * [c])")]
    [TestCase("not x and y", "((not [x]) and [y])")]
    [TestCase("x ? 1 : 2.5", "([x] ? 1 : 2.5)")]
    [TestCase("coalesce([Fire Rating], 'none')", "coalesce([Fire Rating], 'none')")]
    [TestCase("'it\\'s' & \"a\"", "('it\\'s' & 'a')")]
    [TestCase("[odd]]name] == null", "([odd]]name] == null)")]
    [TestCase("-1.5e10", "(- 15000000000)")]
    public void RendersCanonically(string text, string expected)
        => Assert.That(text.ParseExpr().Render(), Is.EqualTo(expected));

    [TestCase("a + b * c - d / e % f")]
    [TestCase("len(lower([Name])) >= 3 or startswith([Name], 'x\\ty')")]
    [TestCase("a ? b ? 1 : 2 : 3")]
    public void RoundTrips(string text)
    {
        var once = text.ParseExpr().Render();
        Assert.That(once.ParseExpr().Render(), Is.EqualTo(once));
    }
}
