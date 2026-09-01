using BimOpenFlow.Nodes.Effects;
using ClosedXML.Excel;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportXlsxTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    [Test]
    public void WritesHeaderAndCellsWithBoldHeader()
    {
        var path = Path.Combine(_dir, "out.xlsx");
        var outputs = new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()), Params(("path", path)));

        using var workbook = new XLWorkbook(path);
        var ws = workbook.Worksheet("Sheet1");
        Assert.That(ws.Cell(1, 1).GetString(), Is.EqualTo("name"));
        Assert.That(ws.Cell(1, 4).GetString(), Is.EqualTo("flag"));
        Assert.That(ws.Cell(1, 1).Style.Font.Bold, Is.True);
        Assert.That(ws.Cell(2, 1).GetString(), Is.EqualTo("plain"));
        Assert.That(ws.Cell(2, 2).GetDouble(), Is.EqualTo(1));
        Assert.That(ws.Cell(2, 3).GetDouble(), Is.EqualTo(0.5));
        Assert.That(ws.Cell(2, 4).GetBoolean(), Is.True);
        Assert.That(ws.Cell(3, 2).IsEmpty(), Is.True, "null count cell stays blank");
        Assert.That(ws.LastRowUsed()!.RowNumber(), Is.EqualTo(4));

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "sheet"), Is.EqualTo("Sheet1"));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void HeaderBoldFalseWritesPlainHeader()
    {
        var path = Path.Combine(_dir, "out.xlsx");
        new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("headerBold", "false")));
        using var workbook = new XLWorkbook(path);
        Assert.That(workbook.Worksheet("Sheet1").Cell(1, 1).Style.Font.Bold, Is.False);
    }

    [Test]
    public void ReplaceFileDropsOtherSheets()
    {
        var path = Path.Combine(_dir, "out.xlsx");
        CreateWorkbook(path, ("Old", "stale"));
        new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("sheet", "Data")));
        using var workbook = new XLWorkbook(path);
        Assert.That(workbook.Worksheets.Select(w => w.Name), Is.EqualTo(new[] { "Data" }));
    }

    [Test]
    public void ReplaceSheetPreservesOtherSheetsAndRefreshesTarget()
    {
        var path = Path.Combine(_dir, "out.xlsx");
        CreateWorkbook(path, ("Keep", "precious"), ("Data", "stale"));
        new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("sheet", "Data"), ("mode", "replaceSheet")));

        using var workbook = new XLWorkbook(path);
        Assert.That(workbook.Worksheets.Select(w => w.Name), Is.EquivalentTo(new[] { "Keep", "Data" }));
        Assert.That(workbook.Worksheet("Keep").Cell(1, 1).GetString(), Is.EqualTo("precious"));
        var data = workbook.Worksheet("Data");
        Assert.That(data.Cell(1, 1).GetString(), Is.EqualTo("name"));
        Assert.That(data.LastRowUsed()!.RowNumber(), Is.EqualTo(4), "stale content is gone");
    }

    [Test]
    public void ReplaceSheetCreatesFileWhenAbsent()
    {
        var path = Path.Combine(_dir, "fresh.xlsx");
        new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("mode", "replaceSheet")));
        using var workbook = new XLWorkbook(path);
        Assert.That(workbook.Worksheet("Sheet1").Cell(2, 1).GetString(), Is.EqualTo("plain"));
    }

    [Test]
    public void EmptySheetNameThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportXlsxNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", Path.Combine(_dir, "x.xlsx")), ("sheet", ""))))!.Message,
            Does.StartWith("sink.exportXlsx: "));

    private static void CreateWorkbook(string path, params (string Sheet, string CellA1)[] sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var (sheet, value) in sheets)
            workbook.AddWorksheet(sheet).Cell(1, 1).Value = value;
        workbook.SaveAs(path);
    }
}
