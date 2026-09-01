using System.Collections.Concurrent;
using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using ClosedXML.Excel;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads one worksheet of an .xlsx workbook into a table; the first
/// row is the header. Cached by file content hash.</summary>
public sealed class XlsxReadNode : IFlowNode
{
    public const string Kind = "xlsx.read";

    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sheet", ParamKind.Text, ""),
            new ParamSpec("headerRow", ParamKind.Integer, "1"),
            new ParamSpec("range", ParamKind.Text, ""),
        ],
        "Reads a worksheet (named, or the first) from an .xlsx file. headerRow is the header's row within the range (rows above are skipped); range is an A1-style rectangle like B3:F100 (empty = used range).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var sheet = parameters.GetText("sheet");
        var headerRow = parameters.GetInteger("headerRow", 1);
        if (headerRow < 1)
            throw new ArgumentException($"{Kind}: 'headerRow' must be 1 or greater.");
        var range = parameters.GetText("range").Trim();
        var table = Cache.GetOrAdd($"{FileHashes.HashFile(path)}:{sheet}:{headerRow}:{range}",
            _ => Load(path, sheet, (int)headerRow, range));
        return [new TableValue(table)];
    }

    private static IDataTable Load(string path, string sheet, int headerRow, string range)
    {
        using var workbook = new XLWorkbook(path);
        var worksheet = FindSheet(workbook, sheet);
        var builder = new DataTableBuilder(worksheet.Name);
        if (Region(worksheet, range) is not var (firstRow, lastRow, firstCol, lastCol))
            return builder.Build();

        firstRow += headerRow - 1;
        if (firstRow > lastRow)
            throw new ArgumentException($"{Kind}: 'headerRow' {headerRow} is past the last row of the range.");

        for (var col = firstCol; col <= lastCol; col++)
        {
            var header = worksheet.Cell(firstRow, col).GetString().Trim();
            var name = header.Length > 0 ? header : $"Column{col - firstCol + 1}";
            var cells = new object?[lastRow - firstRow];
            for (var row = firstRow + 1; row <= lastRow; row++)
                cells[row - firstRow - 1] = CellValue(worksheet.Cell(row, col).Value);
            var type = InferType(cells);
            if (type == typeof(string))
                for (var i = 0; i < cells.Length; i++)
                    cells[i] = TableOps.CanonicalText(cells[i]);
            builder.AddColumn(cells, name, type);
        }
        return builder.Build();
    }

    /// <summary>The rectangle to read: the parsed A1-style range, or the used
    /// range (null when the sheet is empty and no range was given).</summary>
    private static (int FirstRow, int LastRow, int FirstCol, int LastCol)? Region(IXLWorksheet worksheet, string range)
    {
        if (range.Length == 0)
        {
            var used = worksheet.RangeUsed();
            return used == null ? null : Corners(used.RangeAddress);
        }
        try
        {
            var address = worksheet.Range(range).RangeAddress;
            return address.IsValid
                ? Corners(address)
                : throw new FormatException();
        }
        catch (Exception e)
        {
            throw new ArgumentException($"{Kind}: invalid range '{range}'; expected an A1-style rectangle like B3:F100.", e);
        }
    }

    private static (int, int, int, int) Corners(IXLRangeAddress address)
        => (address.FirstAddress.RowNumber, address.LastAddress.RowNumber,
            address.FirstAddress.ColumnNumber, address.LastAddress.ColumnNumber);

    private static IXLWorksheet FindSheet(XLWorkbook workbook, string sheet)
        => sheet.Length == 0
            ? workbook.Worksheets.First()
            : workbook.Worksheets.TryGetWorksheet(sheet, out var found)
                ? found
                : throw new ArgumentException($"{Kind}: no worksheet named '{sheet}'.");

    /// <summary>Cell to CLR value: bool, double, ISO-8601 text for dates, text; null when blank.</summary>
    private static object? CellValue(XLCellValue value)
        => value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Boolean => value.GetBoolean(),
            XLDataType.Number => value.GetNumber(),
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => value.GetTimeSpan().ToString(null, CultureInfo.InvariantCulture),
            XLDataType.Error => null,
            _ => value.GetText(),
        };

    /// <summary>All non-null cells one CLR type => that type; otherwise text.</summary>
    private static Type InferType(IReadOnlyList<object?> cells)
    {
        Type? found = null;
        foreach (var cell in cells)
        {
            if (cell == null)
                continue;
            var t = cell.GetType();
            if (found == null)
                found = t;
            else if (found != t)
                return typeof(string);
        }
        return found ?? typeof(string);
    }
}
