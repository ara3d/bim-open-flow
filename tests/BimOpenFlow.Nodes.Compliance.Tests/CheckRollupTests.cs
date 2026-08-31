using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Compliance;
using static BimOpenFlow.Nodes.Compliance.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Compliance.Tests;

[TestFixture]
public sealed class CheckRollupTests
{
    private static readonly CheckRollupNode Node = new();
    private static readonly CheckUnionNode Union = new();

    [Test]
    public void CountsVerdictsPerCheckId()
    {
        var a = VerdictTable("C1", "First", "Cite 1",
            Verdict.Pass, Verdict.Fail, Verdict.Pass, Verdict.InfoNotAvailable);
        var b = VerdictTable("C2", "Second", "Cite 2", Verdict.NeedsReview, Verdict.Pass);
        var output = Node.EvalTable(ParamValues.Empty, Union.EvalTable(ParamValues.Empty, a, b));

        Assert.That(output.Rows.Count, Is.EqualTo(2));
        Assert.That(output.ColumnNames(), Is.EqualTo(new[]
        {
            "checkId", "checkTitle", "citation",
            "passCount", "failCount", "needsReviewCount", "infoNotAvailableCount", "worst",
        }));
        Assert.That(output.Cell("checkId", 0), Is.EqualTo("C1"));
        Assert.That(output.Cell("checkTitle", 0), Is.EqualTo("First"));
        Assert.That(output.Cell("citation", 0), Is.EqualTo("Cite 1"));
        Assert.That(output.Cell("passCount", 0), Is.EqualTo(2L));
        Assert.That(output.Cell("failCount", 0), Is.EqualTo(1L));
        Assert.That(output.Cell("needsReviewCount", 0), Is.EqualTo(0L));
        Assert.That(output.Cell("infoNotAvailableCount", 0), Is.EqualTo(1L));
        Assert.That(output.Cell("checkId", 1), Is.EqualTo("C2"));
        Assert.That(output.Cell("passCount", 1), Is.EqualTo(1L));
        Assert.That(output.Cell("needsReviewCount", 1), Is.EqualTo(1L));
    }

    [TestCase(Verdict.Pass, Verdict.Pass, "Pass")]
    [TestCase(Verdict.Pass, Verdict.InfoNotAvailable, "InfoNotAvailable")]
    [TestCase(Verdict.InfoNotAvailable, Verdict.NeedsReview, "NeedsReview")]
    [TestCase(Verdict.NeedsReview, Verdict.Fail, "Fail")]
    [TestCase(Verdict.Fail, Verdict.Pass, "Fail")]
    public void WorstFollowsSeverityOrder(Verdict first, Verdict second, string expected)
    {
        var output = Node.EvalTable(ParamValues.Empty, VerdictTable("C1", "T", "C", first, second));
        Assert.That(output.Cell("worst", 0), Is.EqualTo(expected));
    }

    [Test]
    public void EmptyVerdictTableYieldsEmptySummary()
    {
        var output = Node.EvalTable(ParamValues.Empty, VerdictTable("C1", "T", "C"));
        Assert.That(output.Rows.Count, Is.EqualTo(0));
    }

    [Test]
    public void NonVerdictTableThrows()
        => Assert.Throws<ArgumentException>(() => Node.EvalTable(ParamValues.Empty,
            Table("plain", ("height", typeof(double), new object?[] { 1.0 }))));

    [Test]
    public void UnknownVerdictTextThrows()
    {
        var table = Table("bad",
            ("verdict", typeof(string), new object?[] { "Maybe" }),
            ("checkId", typeof(string), new object?[] { "C1" }),
            ("checkTitle", typeof(string), new object?[] { "T" }),
            ("citation", typeof(string), new object?[] { "C" }));
        Assert.Throws<ArgumentException>(() => Node.EvalTable(ParamValues.Empty, table));
    }
}
