using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DateFilterNodeTests
{
    private static TableValue Dates()
        => TestTables.TextColumn("d", "2024-01-01", "2024-01-15", "2024-02-01");

    [Test]
    public void Half_Open_Range_Includes_From_Excludes_To()
    {
        var table = new DateFilterNode().EvalTable([Dates()],
            ("column", "d"), ("from", "2024-01-01"), ("to", "2024-02-01"));
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2024-01-15"));
    }

    [Test]
    public void Open_Start_Keeps_Everything_Before_To()
    {
        var table = new DateFilterNode().EvalTable([Dates()],
            ("column", "d"), ("to", "2024-01-15"));
        Assert.That(table.Rows.Count, Is.EqualTo(1));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01"));
    }

    [Test]
    public void Open_End_Keeps_Everything_From_From()
    {
        var table = new DateFilterNode().EvalTable([Dates()],
            ("column", "d"), ("from", "2024-01-15"));
        Assert.That(table.Rows.Count, Is.EqualTo(2));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-15"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2024-02-01"));
    }

    [Test]
    public void Datetime_Bound_Filters_Within_A_Day()
    {
        var input = TestTables.TextColumn("d", "2024-01-01T08:00:00", "2024-01-01T18:00:00");
        var table = new DateFilterNode().EvalTable([input],
            ("column", "d"), ("from", "2024-01-01T09:00:00"));
        Assert.That(table.Rows.Count, Is.EqualTo(1));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01T18:00:00"));
    }

    [Test]
    public void Both_Bounds_Empty_Warns_And_Passes_Through()
    {
        var context = new FakeEvalContext();
        var input = Dates();
        var result = new DateFilterNode().Eval(context, [input],
            NodeTestHelpers.Params(("column", "d")));
        Assert.That(((TableValue)result[0]).Table.Rows.Count, Is.EqualTo(3));
        Assert.That(context.Warnings, Has.One.Contains("date.filter"));
    }

    [Test]
    public void Invalid_Bound_Text_Is_An_Error()
    {
        Assert.That(
            () => new DateFilterNode().EvalTable([Dates()], ("column", "d"), ("from", "01/02/2024")),
            Throws.ArgumentException.With.Message.StartsWith("date.filter: "));
    }

    [Test]
    public void Non_Iso_Column_Errors_Pointing_To_Parse()
    {
        var input = TestTables.TextColumn("d", "next week");
        Assert.That(
            () => new DateFilterNode().EvalTable([input], ("column", "d"), ("from", "2024-01-01")),
            Throws.ArgumentException.With.Message.StartsWith("date.filter: ")
                .And.Message.Contains("date.parse"));
    }
}
