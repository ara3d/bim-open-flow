namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DateDiffNodeTests
{
    [Test]
    public void Days_Is_The_Default_Unit()
    {
        var input = TestTables.TwoTextColumns(
            ("a", ["2024-01-01"]), ("b", ["2024-01-31"]));
        var table = new DateDiffNode().EvalTable([input], ("a", "a"), ("b", "b"), ("name", "n"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "a", "b", "n" }));
        Assert.That(table.Cell("n", 0), Is.EqualTo(30L));
    }

    [Test]
    public void Negative_When_B_Is_Earlier()
    {
        var input = TestTables.TwoTextColumns(
            ("a", ["2024-01-31"]), ("b", ["2024-01-01"]));
        var table = new DateDiffNode().EvalTable([input],
            ("a", "a"), ("b", "b"), ("unit", "days"), ("name", "n"));
        Assert.That(table.Cell("n", 0), Is.EqualTo(-30L));
    }

    [Test]
    public void Months_Counts_Boundaries_Crossed()
    {
        var input = TestTables.TwoTextColumns(
            ("a", ["2024-01-31", "2024-01-01"]), ("b", ["2024-02-01", "2024-01-31"]));
        var table = new DateDiffNode().EvalTable([input],
            ("a", "a"), ("b", "b"), ("unit", "months"), ("name", "n"));
        Assert.That(table.Cell("n", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("n", 1), Is.EqualTo(0L));
    }

    [Test]
    public void Hours_Between_Datetimes()
    {
        var input = TestTables.TwoTextColumns(
            ("a", ["2024-01-01T08:00:00"]), ("b", ["2024-01-01T17:30:00"]));
        var table = new DateDiffNode().EvalTable([input],
            ("a", "a"), ("b", "b"), ("unit", "hours"), ("name", "n"));
        Assert.That(table.Cell("n", 0), Is.EqualTo(9L));
    }

    [Test]
    public void Missing_Name_Is_An_Error()
    {
        var input = TestTables.TwoTextColumns(("a", ["2024-01-01"]), ("b", ["2024-01-02"]));
        Assert.That(
            () => new DateDiffNode().EvalTable([input], ("a", "a"), ("b", "b")),
            Throws.ArgumentException.With.Message.StartsWith("date.diff: "));
    }

    [Test]
    public void Non_Iso_Column_Errors_Pointing_To_Parse()
    {
        var input = TestTables.TwoTextColumns(("a", ["2024-01-01"]), ("b", ["soon"]));
        Assert.That(
            () => new DateDiffNode().EvalTable([input], ("a", "a"), ("b", "b"), ("name", "n")),
            Throws.ArgumentException.With.Message.StartsWith("date.diff: ")
                .And.Message.Contains("date.parse"));
    }
}
