using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Compliance;
using static BimOpenFlow.Nodes.Compliance.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Compliance.Tests;

[TestFixture]
public sealed class CheckRuleTests
{
    private static readonly CheckRuleNode Node = new();

    private static ParamValues RuleParams(string expr, string reviewExpr = "")
        => Params(
            ("checkId", "NBC-9.5.3.1"),
            ("title", "Door clear width"),
            ("citation", "NBC 2020 9.5.3.1.(1)"),
            ("expr", expr),
            ("reviewExpr", reviewExpr));

    private static readonly (string, Type, object?[]) Heights =
        ("height", typeof(double), new object?[] { 2.5, 1.0, null });

    [Test]
    public void TrueIsPassFalseIsFailNullIsInfoNotAvailable()
    {
        var output = Node.EvalTable(RuleParams("height >= 2.0"), Table("doors", Heights));
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail", "InfoNotAvailable" }));
    }

    [Test]
    public void ReviewExprTurnsFailIntoNeedsReview()
    {
        var table = Table("doors",
            ("height", typeof(double), new object?[] { 2.5, 1.0, 1.9, null }),
            ("existing", typeof(bool), new object?[] { false, true, false, true }));
        var output = Node.EvalTable(RuleParams("height >= 2.0", "existing"), table);
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "NeedsReview", "Fail", "InfoNotAvailable" }));
    }

    [Test]
    public void OutputAppendsVerdictAndMetadataColumns()
    {
        var output = Node.EvalTable(RuleParams("height >= 2.0"), Table("doors", Heights));
        Assert.That(output.ColumnNames(),
            Is.EqualTo(new[] { "height", "verdict", "checkId", "checkTitle", "citation" }));
        Assert.That(output.Cell("height", 0), Is.EqualTo(2.5));
        Assert.That(output.Cell("height", 2), Is.Null);
        Assert.That(output.Cell("checkId", 1), Is.EqualTo("NBC-9.5.3.1"));
        Assert.That(output.Cell("checkTitle", 1), Is.EqualTo("Door clear width"));
        Assert.That(output.Cell("citation", 1), Is.EqualTo("NBC 2020 9.5.3.1.(1)"));
        Assert.That(output.Name, Is.EqualTo("NBC-9.5.3.1"));
    }

    [Test]
    public void MixedColumnTypesEvaluate()
    {
        var table = Table("walls",
            ("count", typeof(long), new object?[] { 3L, 1L }),
            ("material", typeof(string), new object?[] { "Concrete", "Wood" }));
        var output = Node.EvalTable(RuleParams("count > 2 and material == 'Concrete'"), table);
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail" }));
    }

    [Test]
    public void UnknownColumnThrows()
        => Assert.Throws<ArgumentException>(
            () => Node.EvalTable(RuleParams("width >= 2.0"), Table("doors", Heights)));

    [Test]
    public void NonBooleanExprThrows()
        => Assert.Throws<ArgumentException>(
            () => Node.EvalTable(RuleParams("height + 1"), Table("doors", Heights)));

    [Test]
    public void ReservedColumnCollisionThrows()
    {
        var table = Table("doors",
            ("height", typeof(double), new object?[] { 2.5 }),
            ("verdict", typeof(string), new object?[] { "Pass" }));
        Assert.Throws<ArgumentException>(() => Node.EvalTable(RuleParams("height >= 2.0"), table));
    }
}
