using ClosedXML.Excel;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>xlsx.sheets against a generated workbook: names, positions, used
/// extents, empty sheets, and errors.</summary>
[TestFixture]
public sealed class XlsxSheetsNodeTests
{
    private string _dir = "";
    private string _path = "";

    [OneTimeSetUp]
    public void CreateWorkbook()
    {
        _dir = Directory.CreateTempSubdirectory("tables-xlsx-sheets-").FullName;
        _path = Path.Combine(_dir, "book.xlsx");
        using var workbook = new XLWorkbook();
        var first = workbook.AddWorksheet("First");
        first.Cell(1, 1).Value = "A";
        first.Cell(1, 2).Value = "B";
        first.Cell(2, 1).Value = 1.0;
        first.Cell(3, 1).Value = 2.0;
        workbook.AddWorksheet("Empty");
        workbook.SaveAs(_path);
    }

    [OneTimeTearDown]
    public void DeleteWorkbook()
        => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Sheets_NamesIndicesAndUsedExtents()
    {
        var table = new XlsxSheetsNode().EvalTable([], ("path", _path));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "index", "rowCount", "columnCount" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("name", 0), Is.EqualTo("First"));
        Assert.That(table.Cell("index", 0), Is.EqualTo(1L));
        Assert.That(table.Cell("rowCount", 0), Is.EqualTo(3L));
        Assert.That(table.Cell("columnCount", 0), Is.EqualTo(2L));
        Assert.That(table.Cell("name", 1), Is.EqualTo("Empty"));
        Assert.That(table.Cell("rowCount", 1), Is.EqualTo(0L));
        Assert.That(table.Cell("columnCount", 1), Is.EqualTo(0L));
    }

    [Test]
    public void Sheets_MissingFileOrPath_Throw()
    {
        Assert.That(() => new XlsxSheetsNode().EvalTable([], ("path", Path.Combine(_dir, "nope.xlsx"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.StartsWith("xlsx.sheets: "));
        Assert.That(() => new XlsxSheetsNode().EvalTable([]),
            Throws.ArgumentException.With.Message.StartsWith("xlsx.sheets: "));
    }
}
