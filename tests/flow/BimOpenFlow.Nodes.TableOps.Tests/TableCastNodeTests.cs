using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.TableOps.Tests;

[TestFixture]
public class TableCastNodeTests
{
    [Test]
    public void Casts_Text_To_Integer_In_Place()
    {
        var input = NodeTestHelpers.Table(("n", typeof(string), ["1", "2", "3"]));
        var table = new TableCastNode().EvalTable([input], ("column", "n"), ("type", "integer"));
        Assert.That(table.ColumnCells("n"), Is.EqualTo(new[] { 1L, 2L, 3L }));
    }

    [Test]
    public void OnError_Null_Uses_TryCast_And_Warns_With_The_Count()
    {
        var input = NodeTestHelpers.Table(("n", typeof(string), ["1", "oops", "bad"]));
        var (table, warnings) = new TableCastNode().EvalWithWarnings([input],
            ("column", "n"), ("type", "integer"), ("onError", "null"));
        Assert.That(table.Cell("n", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("n", 1), Is.Null.Or.EqualTo(DBNull.Value));
        Assert.That(warnings, Has.One.Contains("2"));
    }

    [Test]
    public void OnError_Error_Throws_On_A_Bad_Value()
    {
        var input = NodeTestHelpers.Table(("n", typeof(string), ["oops"]));
        Assert.That(
            () => new TableCastNode().EvalTable([input], ("column", "n"), ("type", "integer")),
            Throws.Exception);
    }

    [Test]
    public void Date_Cast_Accepts_Iso_And_Returns_Iso_Text()
    {
        var input = NodeTestHelpers.Table(("d", typeof(string), ["2026-01-31"]));
        var table = new TableCastNode().EvalTable([input], ("column", "d"), ("type", "date"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2026-01-31"));
    }

    [Test]
    public void Named_Result_Adds_A_New_Column()
    {
        var input = NodeTestHelpers.Table(("n", typeof(string), ["1"]));
        var table = new TableCastNode().EvalTable([input],
            ("column", "n"), ("type", "number"), ("name", "value"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "n", "value" }));
        Assert.That(table.Cell("value", 0), Is.EqualTo(1.0));
    }

    [Test]
    public void Existing_New_Name_Is_An_Error()
    {
        var input = NodeTestHelpers.Table(
            ("n", typeof(string), ["1"]), ("value", typeof(long), [2L]));
        Assert.That(
            () => new TableCastNode().EvalTable([input],
                ("column", "n"), ("type", "number"), ("name", "value")),
            Throws.ArgumentException.With.Message.StartsWith("table.cast:"));
    }
}
