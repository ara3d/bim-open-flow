namespace BimOpenFlow.Nodes.Dates.Tests;

[TestFixture]
public class DatePartNodeTests
{
    [Test]
    public void Extracts_Year_Month_And_Iso_DayOfWeek()
    {
        var input = TestTables.TextColumn("d", "2024-01-01", "2023-12-31");
        var node = new DatePartNode();

        var year = node.EvalTable([input], ("column", "d"), ("part", "year"), ("name", "y"));
        Assert.That(year.Cell("y", 0), Is.EqualTo(2024L));
        Assert.That(year.Cell("y", 1), Is.EqualTo(2023L));

        var month = node.EvalTable([input], ("column", "d"), ("part", "month"), ("name", "m"));
        Assert.That(month.Cell("m", 1), Is.EqualTo(12L));

        // 2024-01-01 was a Monday, 2023-12-31 a Sunday.
        var dow = node.EvalTable([input], ("column", "d"), ("part", "dayOfWeek"), ("name", "dow"));
        Assert.That(dow.Cell("dow", 0), Is.EqualTo(1L));
        Assert.That(dow.Cell("dow", 1), Is.EqualTo(7L));
    }

    [Test]
    public void Extracts_Time_Parts_From_Datetime()
    {
        var input = TestTables.TextColumn("d", "2024-03-15T10:45:12");
        var hour = new DatePartNode().EvalTable([input], ("column", "d"), ("part", "hour"), ("name", "h"));
        Assert.That(hour.Cell("h", 0), Is.EqualTo(10L));
    }

    [Test]
    public void Appends_Column_And_Keeps_Input_Columns()
    {
        var input = TestTables.TextColumn("d", "2024-05-05");
        var table = new DatePartNode().EvalTable([input], ("column", "d"), ("part", "quarter"), ("name", "q"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "d", "q" }));
        Assert.That(table.Cell("q", 0), Is.EqualTo(2L));
    }

    [Test]
    public void Missing_Name_Is_An_Error()
    {
        var input = TestTables.TextColumn("d", "2024-01-01");
        Assert.That(
            () => new DatePartNode().EvalTable([input], ("column", "d"), ("part", "year")),
            Throws.ArgumentException.With.Message.StartsWith("date.part: "));
    }

    [Test]
    public void Non_Iso_Column_Errors_Pointing_To_Parse()
    {
        var input = TestTables.TextColumn("d", "31/01/2024");
        Assert.That(
            () => new DatePartNode().EvalTable([input], ("column", "d"), ("part", "year"), ("name", "y")),
            Throws.ArgumentException.With.Message.StartsWith("date.part: ")
                .And.Message.Contains("date.parse"));
    }
}
