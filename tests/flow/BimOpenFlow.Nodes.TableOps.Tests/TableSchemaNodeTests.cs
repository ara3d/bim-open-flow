using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableSchemaNodeTests
{
    [Test]
    public void Outputs_Column_Type_And_Index()
    {
        var input = NodeTestHelpers.Table(
            ("flag", typeof(bool), [true]),
            ("n", typeof(long), [1L]),
            ("x", typeof(double), [1.5]),
            ("s", typeof(string), ["a"]));
        var table = new TableSchemaNode().EvalTable([input]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "column", "type", "index" }));
        Assert.That(table.ColumnCells("column"), Is.EqualTo(new[] { "flag", "n", "x", "s" }));
        Assert.That(table.ColumnCells("type"),
            Is.EqualTo(new[] { "Boolean", "Integer", "Number", "Text" }));
        Assert.That(table.ColumnCells("index"), Is.EqualTo(new[] { 0L, 1L, 2L, 3L }));
    }
}
