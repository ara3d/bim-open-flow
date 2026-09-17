using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DateParseNodeTests
{
    [Test]
    public void Parses_Format_In_Place()
    {
        var input = TestTables.TextColumn("d", "31/01/2024", "15/02/2024");
        var table = new DateParseNode().EvalTable([input], ("column", "d"), ("format", "%d/%m/%Y"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d" }));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-31"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2024-02-15"));
    }

    [Test]
    public void Empty_Format_Accepts_Iso_Date_And_Datetime()
    {
        var input = TestTables.TextColumn("d", "2024-01-31", "2024-01-31T10:30:00");
        var table = new DateParseNode().EvalTable([input], ("column", "d"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-31"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2024-01-31T10:30:00"));
    }

    [Test]
    public void OnError_Null_Nulls_And_Warns_With_Count()
    {
        var context = new FakeEvalContext();
        var input = TestTables.TextColumn("d", "2024-01-01", "bogus", null);
        var result = new DateParseNode().Eval(context, [input],
            NodeTestHelpers.Params(("column", "d"), ("onError", "null")));
        var table = ((TableValue)result[0]).Table;
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01"));
        Assert.That(table.Cell("d", 1), Is.Null);
        Assert.That(table.Cell("d", 2), Is.Null);
        Assert.That(context.Warnings, Has.One.Contains("1 unparseable"));
    }

    [Test]
    public void OnError_Error_Throws_With_Kind_Prefix()
    {
        var input = TestTables.TextColumn("d", "bogus");
        Assert.That(
            () => new DateParseNode().EvalTable([input], ("column", "d")),
            Throws.ArgumentException.With.Message.StartsWith("date.parse: "));
    }

    [Test]
    public void Named_Column_Appends_And_Keeps_Source()
    {
        var input = TestTables.TextColumn("d", "01/06/2024");
        var table = new DateParseNode().EvalTable([input],
            ("column", "d"), ("format", "%d/%m/%Y"), ("name", "parsed"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d", "parsed" }));
        Assert.That(table.Cell("d", 0), Is.EqualTo("01/06/2024"));
        Assert.That(table.Cell("parsed", 0), Is.EqualTo("2024-06-01"));
    }

    [Test]
    public void Existing_Name_Is_An_Error()
    {
        var input = TestTables.TextColumn("d", "2024-01-01");
        Assert.That(
            () => new DateParseNode().EvalTable([input], ("column", "d"), ("name", "d")),
            Throws.ArgumentException.With.Message.StartsWith("date.parse: "));
    }

    [Test]
    public void Missing_Column_Is_An_Error()
    {
        var input = TestTables.TextColumn("d", "2024-01-01");
        Assert.That(
            () => new DateParseNode().EvalTable([input], ("column", "nope")),
            Throws.ArgumentException.With.Message.StartsWith("date.parse: "));
    }
}
