using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using ClosedXML.Excel;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportXlsx: writes the input table to one sheet of an Excel
/// workbook — either a fresh single-sheet file or one refreshed sheet of an
/// existing workbook.</summary>
public sealed class ExportXlsxNode : IFlowNode
{
    public const string Kind = "sink.exportXlsx";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sheet", ParamKind.Text, "Sheet1"),
            new ParamSpec("mode", ParamKind.Enum, "replaceFile", new[] { "replaceFile", "replaceSheet" }),
            new ParamSpec("autoWidth", ParamKind.Boolean, "true"),
            new ParamSpec("headerBold", ParamKind.Boolean, "true"),
        },
        "Writes the input table to an Excel workbook sheet. 'replaceFile' writes a fresh single-sheet workbook; 'replaceSheet' refreshes one sheet of an existing workbook, leaving other sheets alone (the file is created if absent). Outputs a one-row summary (path, rowCount, sheet).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        var sheet = parameters.GetText("sheet", "Sheet1");
        if (sheet.Length == 0)
            throw new ArgumentException($"{Kind}: parameter 'sheet' must be non-empty");
        var mode = parameters.GetEnum("mode", Kind, "replaceFile", "replaceFile", "replaceSheet");
        var autoWidth = parameters.GetBoolean("autoWidth", true);
        var headerBold = parameters.GetBoolean("headerBold", true);
        Sinks.ReplaceVia(path, temp =>
            Save(table, Path.GetFullPath(path), temp, sheet, mode, autoWidth, headerBold));
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportXlsx",
                ("path", path),
                ("rowCount", (long)table.Rows.Count),
                ("sheet", sheet))),
        };
    }

    private static void Save(IDataTable table, string target, string temp, string sheet, string mode, bool autoWidth, bool headerBold)
    {
        using var workbook = mode == "replaceSheet" && File.Exists(target)
            ? new XLWorkbook(target)
            : new XLWorkbook();
        var worksheet = workbook.Worksheets.TryGetWorksheet(sheet, out var existing)
            ? existing
            : workbook.AddWorksheet(sheet);
        worksheet.Clear();
        Fill(worksheet, table);
        if (headerBold && table.Columns.Count > 0)
            worksheet.Range(1, 1, 1, table.Columns.Count).Style.Font.SetBold();
        if (autoWidth && table.Columns.Count > 0)
            worksheet.Columns(1, table.Columns.Count).AdjustToContents();
        workbook.SaveAs(temp);
    }

    private static void Fill(IXLWorksheet worksheet, IDataTable table)
    {
        for (var c = 0; c < table.Columns.Count; c++)
            worksheet.Cell(1, c + 1).Value = table.Columns[c].Descriptor.Name;
        for (var r = 0; r < table.Rows.Count; r++)
            for (var c = 0; c < table.Columns.Count; c++)
                worksheet.Cell(r + 2, c + 1).Value = ToCellValue(table[c, r]);
    }

    private static XLCellValue ToCellValue(object? value)
        => value switch
        {
            null => Blank.Value,
            bool b => b,
            string s => s,
            sbyte or byte or short or ushort or int or uint or long or ulong
                or float or double or decimal => Convert.ToDouble(value),
            _ => CsvWriting.FormatCell(value),
        };
}
