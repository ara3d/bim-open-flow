namespace BimOpenFlow.Publishing.Tests;

public class HtmlTablesTests
{
    [Test]
    public void ToHtml_EscapesCellsAndHeaders()
    {
        var html = TestTable.Sample().ToHtml();
        Assert.That(html, Does.Contain("Wall &lt;A&gt;"));
        Assert.That(html, Does.Contain("Door &quot;B&quot;"));
        Assert.That(html, Does.Not.Contain("Wall <A>"));
    }

    [Test]
    public void ToHtml_RightAlignsNumericColumns()
    {
        var html = TestTable.Sample().ToHtml();
        Assert.That(html, Does.Contain("<td class=\"bof-num\">3</td>"));
        Assert.That(html, Does.Contain("<td class=\"bof-num\">1.5</td>"));
    }

    [Test]
    public void ToHtml_CapsRowsWithNote()
    {
        var html = TestTable.Sample().ToHtml(maxRows: 2);
        Assert.That(html, Does.Contain("Showing 2 of 3 rows"));
        Assert.That(html, Does.Not.Contain("2.25"));
    }

    [Test]
    public void ToHtml_UncappedTableHasNoNote()
        => Assert.That(TestTable.Sample().ToHtml(), Does.Not.Contain("Showing"));

    [Test]
    public void FormatCell_IsInvariant()
    {
        Assert.That(HtmlTables.FormatCell(1234.5), Is.EqualTo("1234.5"));
        Assert.That(HtmlTables.FormatCell(true), Is.EqualTo("true"));
        Assert.That(HtmlTables.FormatCell(null), Is.EqualTo(""));
        Assert.That(HtmlTables.FormatCell(42L), Is.EqualTo("42"));
    }
}
