using ClosedXML.Excel;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>xlsx.read against a workbook generated into a temp directory:
/// header row, type inference, blank cells, sheet selection, and errors.</summary>
[TestFixture]
public sealed class XlsxReadNodeTests
{
    private string _dir = "";
    private string _path = "";

    [OneTimeSetUp]
    public void CreateWorkbook()
    {
        _dir = Directory.CreateTempSubdirectory("tables-xlsx-").FullName;
        _path = Path.Combine(_dir, "book.xlsx");
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("People");
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Age";
        ws.Cell(1, 3).Value = "Active";
        ws.Cell(1, 4).Value = "Born";
        ws.Cell(2, 1).Value = "Alice";
        ws.Cell(2, 2).Value = 30.0;
        ws.Cell(2, 3).Value = true;
        ws.Cell(2, 4).Value = new DateTime(1995, 6, 1);
        ws.Cell(3, 1).Value = "Bob";
        // row 3: Age blank, Active blank
        ws.Cell(3, 4).Value = new DateTime(2000, 12, 31, 8, 30, 0);

        var mixed = workbook.AddWorksheet("Mixed");
        mixed.Cell(1, 1).Value = "V";
        mixed.Cell(2, 1).Value = 1.5;
        mixed.Cell(3, 1).Value = "text";
        workbook.SaveAs(_path);
    }

    [OneTimeTearDown]
    public void DeleteWorkbook()
        => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Read_FirstSheet_HeadersTypesAndNulls()
    {
        var table = new XlsxReadNode().EvalTable([], ("path", _path));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "Age", "Active", "Born" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("Name", 0), Is.EqualTo("Alice"));
        Assert.That(table.Cell("Age", 0), Is.EqualTo(30.0));
        Assert.That(table.Cell("Active", 0), Is.EqualTo(true));
        Assert.That(table.Cell("Born", 0), Is.EqualTo("1995-06-01T00:00:00"));
        Assert.That(table.Cell("Age", 1), Is.Null);
        Assert.That(table.Cell("Active", 1), Is.Null);
        Assert.That(table.Cell("Born", 1), Is.EqualTo("2000-12-31T08:30:00"));
        Assert.That(table.Columns[1].Descriptor.Type, Is.EqualTo(typeof(double)));
        Assert.That(table.Columns[2].Descriptor.Type, Is.EqualTo(typeof(bool)));
        Assert.That(table.Columns[3].Descriptor.Type, Is.EqualTo(typeof(string)));
    }

    [Test]
    public void Read_NamedSheet_MixedTypesLandAsText()
    {
        var table = new XlsxReadNode().EvalTable([], ("path", _path), ("sheet", "Mixed"));
        Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(string)));
        Assert.That(table.Cell("V", 0), Is.EqualTo("1.5"));
        Assert.That(table.Cell("V", 1), Is.EqualTo("text"));
    }

    [Test]
    public void Read_MissingSheet_Throws()
        => Assert.That(() => new XlsxReadNode().EvalTable([], ("path", _path), ("sheet", "Nope")),
            Throws.ArgumentException.With.Message.Contains("Nope"));

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(() => new XlsxReadNode().EvalTable([], ("path", Path.Combine(_dir, "nope.xlsx"))),
            Throws.InstanceOf<FileNotFoundException>());

    [Test]
    public void Read_MissingPath_Throws()
        => Assert.That(() => new XlsxReadNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("path"));
}
