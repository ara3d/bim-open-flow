using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Compliance;
using static BimOpenFlow.Nodes.Compliance.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Compliance.Tests;

[TestFixture]
public sealed class CheckUnionTests
{
    private static readonly CheckUnionNode Node = new();

    [Test]
    public void ConcatenatesRowsOfAThenB()
    {
        var a = VerdictTable("C1", "First", "Cite 1", Verdict.Pass, Verdict.Fail);
        var b = VerdictTable("C2", "Second", "Cite 2", Verdict.NeedsReview);
        var output = Node.EvalTable(ParamValues.Empty, a, b);

        Assert.That(output.Rows.Count, Is.EqualTo(3));
        Assert.That(output.ColumnNames(), Is.EqualTo(a.ColumnNames()));
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail", "NeedsReview" }));
        Assert.That(output.Cell("checkId", 0), Is.EqualTo("C1"));
        Assert.That(output.Cell("checkId", 2), Is.EqualTo("C2"));
    }

    [Test]
    public void ChainsForThreeTables()
    {
        var a = VerdictTable("C1", "T1", "X", Verdict.Pass);
        var b = VerdictTable("C2", "T2", "X", Verdict.Fail);
        var c = VerdictTable("C3", "T3", "X", Verdict.InfoNotAvailable);
        var output = Node.EvalTable(ParamValues.Empty, Node.EvalTable(ParamValues.Empty, a, b), c);
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail", "InfoNotAvailable" }));
    }

    [Test]
    public void MismatchedColumnsThrow()
    {
        var a = VerdictTable("C1", "T1", "X", Verdict.Pass);
        var b = Table("other",
            ("height", typeof(double), new object?[] { 1.0 }),
            ("verdict", typeof(string), new object?[] { "Pass" }),
            ("checkId", typeof(string), new object?[] { "C2" }),
            ("checkTitle", typeof(string), new object?[] { "T2" }),
            ("citation", typeof(string), new object?[] { "X" }));
        Assert.Throws<ArgumentException>(() => Node.EvalTable(ParamValues.Empty, a, b));
    }

    [Test]
    public void NonVerdictTableThrows()
    {
        var a = Table("plain", ("height", typeof(double), new object?[] { 1.0 }));
        Assert.Throws<ArgumentException>(() => Node.EvalTable(ParamValues.Empty, a, a));
    }
}
