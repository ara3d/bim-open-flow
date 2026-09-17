namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DateOffsetNodeTests
{
    [Test]
    public void Month_End_Clamps_On_Month_Offset()
    {
        var input = TestTables.TextColumn("d", "2024-01-31", "2023-01-31");
        var table = new DateOffsetNode().EvalTable([input],
            ("column", "d"), ("amount", "1"), ("unit", "months"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-02-29"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2023-02-28"));
    }

    [Test]
    public void Days_Is_The_Default_Unit_And_Negatives_Go_Back()
    {
        var input = TestTables.TextColumn("d", "2024-03-01");
        var table = new DateOffsetNode().EvalTable([input], ("column", "d"), ("amount", "-1"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-02-29"));
    }

    [Test]
    public void Datetime_Keeps_Its_Time_Of_Day()
    {
        var input = TestTables.TextColumn("d", "2024-01-31T05:30:00");
        var table = new DateOffsetNode().EvalTable([input],
            ("column", "d"), ("amount", "1"), ("unit", "months"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-02-29T05:30:00"));
    }

    [Test]
    public void Named_Column_Appends()
    {
        var input = TestTables.TextColumn("d", "2024-01-01");
        var table = new DateOffsetNode().EvalTable([input],
            ("column", "d"), ("amount", "7"), ("unit", "days"), ("name", "due"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d", "due" }));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01"));
        Assert.That(table.Cell("due", 0), Is.EqualTo("2024-01-08"));
    }

    [Test]
    public void Missing_Amount_Is_An_Error()
    {
        var input = TestTables.TextColumn("d", "2024-01-01");
        Assert.That(
            () => new DateOffsetNode().EvalTable([input], ("column", "d")),
            Throws.ArgumentException.With.Message.StartsWith("date.offset: "));
    }

    [Test]
    public void Non_Iso_Column_Errors_Pointing_To_Parse()
    {
        var input = TestTables.TextColumn("d", "tomorrow");
        Assert.That(
            () => new DateOffsetNode().EvalTable([input], ("column", "d"), ("amount", "1")),
            Throws.ArgumentException.With.Message.StartsWith("date.offset: ")
                .And.Message.Contains("date.parse"));
    }
}
