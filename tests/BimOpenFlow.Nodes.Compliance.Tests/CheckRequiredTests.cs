using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Compliance;
using static BimOpenFlow.Nodes.Compliance.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Compliance.Tests;

[TestFixture]
public sealed class CheckRequiredTests
{
    private static readonly CheckRequiredNode Node = new();

    private static ParamValues RequiredParams(string columns)
        => Params(
            ("checkId", "REQ-1"),
            ("title", "Required data"),
            ("citation", "Spec 1.2"),
            ("columns", columns));

    private static IDataTable Doors()
        => Table("doors",
            ("width", typeof(double), new object?[] { 0.9, null, 0.8 }),
            ("storey", typeof(string), new object?[] { "L1", "L2", null }));

    [Test]
    public void NonNullCellsPassNullCellsFail()
    {
        var output = Node.EvalTable(RequiredParams("width"), Doors());
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail", "Pass" }));
    }

    [Test]
    public void AnyNullAmongMultipleColumnsFails()
    {
        var output = Node.EvalTable(RequiredParams("width, storey"), Doors());
        Assert.That(output.VerdictTexts(), Is.EqualTo(new[] { "Pass", "Fail", "Fail" }));
    }

    [Test]
    public void MissingColumnMakesEveryRowInfoNotAvailable()
    {
        var output = Node.EvalTable(RequiredParams("width, clearance"), Doors());
        Assert.That(output.VerdictTexts(),
            Is.EqualTo(new[] { "InfoNotAvailable", "InfoNotAvailable", "InfoNotAvailable" }));
    }

    [Test]
    public void MissingColumnWarnsWithTheColumnName()
    {
        var context = new StubContext();
        Node.Eval(context, new FlowValue[] { new TableValue(Doors()) }, RequiredParams("clearance"));
        Assert.That(context.Warnings, Has.Count.EqualTo(1));
        Assert.That(context.Warnings[0], Does.Contain("clearance"));
    }

    [Test]
    public void OutputCarriesMetadataColumns()
    {
        var output = Node.EvalTable(RequiredParams("width"), Doors());
        Assert.That(output.ColumnNames(),
            Is.EqualTo(new[] { "width", "storey", "verdict", "checkId", "checkTitle", "citation" }));
        Assert.That(output.Cell("checkId", 0), Is.EqualTo("REQ-1"));
        Assert.That(output.Cell("checkTitle", 0), Is.EqualTo("Required data"));
        Assert.That(output.Cell("citation", 0), Is.EqualTo("Spec 1.2"));
    }

    [Test]
    public void EmptyColumnsParamThrows()
        => Assert.Throws<ArgumentException>(() => Node.EvalTable(RequiredParams(" , "), Doors()));
}
