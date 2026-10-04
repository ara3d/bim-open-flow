namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DateTruncateNodeTests
{
    [Test]
    public void Truncates_To_Month_In_Place()
    {
        var input = TestTables.TextColumn("d", "2024-03-15", "2024-12-31");
        var table = new DateTruncateNode().EvalTable([input], ("column", "d"), ("period", "month"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d" }));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-03-01"));
        Assert.That(table.Cell("d", 1), Is.EqualTo("2024-12-01"));
    }

    [Test]
    public void Truncates_Week_To_Monday()
    {
        // 2024-01-04 was a Thursday; its ISO week started Monday 2024-01-01.
        var input = TestTables.TextColumn("d", "2024-01-04");
        var table = new DateTruncateNode().EvalTable([input], ("column", "d"), ("period", "week"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-01-01"));
    }

    [Test]
    public void Truncates_Datetime_To_Hour_Keeping_Datetime_Text()
    {
        var input = TestTables.TextColumn("d", "2024-03-15T10:45:12");
        var table = new DateTruncateNode().EvalTable([input], ("column", "d"), ("period", "hour"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-03-15T10:00:00"));
    }

    [Test]
    public void Truncating_Datetime_To_Day_Yields_Date_Text()
    {
        var input = TestTables.TextColumn("d", "2024-03-15T10:45:12");
        var table = new DateTruncateNode().EvalTable([input], ("column", "d"), ("period", "day"));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-03-15"));
    }

    [Test]
    public void Named_Column_Appends()
    {
        var input = TestTables.TextColumn("d", "2024-03-15");
        var table = new DateTruncateNode().EvalTable([input],
            ("column", "d"), ("period", "year"), ("name", "y"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d", "y" }));
        Assert.That(table.Cell("d", 0), Is.EqualTo("2024-03-15"));
        Assert.That(table.Cell("y", 0), Is.EqualTo("2024-01-01"));
    }

    [Test]
    public void Non_Iso_Column_Errors_Pointing_To_Parse()
    {
        var input = TestTables.TextColumn("d", "not a date");
        Assert.That(
            () => new DateTruncateNode().EvalTable([input], ("column", "d"), ("period", "month")),
            Throws.ArgumentException.With.Message.StartsWith("date.truncate: ")
                .And.Message.Contains("date.parse"));
    }
}
