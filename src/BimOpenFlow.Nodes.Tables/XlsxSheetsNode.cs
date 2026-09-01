using System.Collections.Concurrent;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using ClosedXML.Excel;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Lists the worksheets of an .xlsx workbook with their used extents.
/// Cached by file content hash.</summary>
public sealed class XlsxSheetsNode : IFlowNode
{
    public const string Kind = "xlsx.sheets";

    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("sheets", PortType.Table)],
        Params: [new ParamSpec("path", ParamKind.FilePath)],
        "Lists the worksheets in an .xlsx file: name, index (1-based), rowCount, columnCount of the used range.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var table = Cache.GetOrAdd(TableOps.ContentHash(path), _ => Load(path));
        return [new TableValue(table)];
    }

    private static IDataTable Load(string path)
    {
        using var workbook = new XLWorkbook(path);
        var names = new List<object?>();
        var indices = new List<object?>();
        var rowCounts = new List<object?>();
        var columnCounts = new List<object?>();
        foreach (var worksheet in workbook.Worksheets)
        {
            var used = worksheet.RangeUsed();
            names.Add(worksheet.Name);
            indices.Add((long)worksheet.Position);
            rowCounts.Add((long)(used?.RowCount() ?? 0));
            columnCounts.Add((long)(used?.ColumnCount() ?? 0));
        }
        var builder = new DataTableBuilder("sheets");
        builder.AddColumn(names.ToArray(), "name", typeof(string));
        builder.AddColumn(indices.ToArray(), "index", typeof(long));
        builder.AddColumn(rowCounts.ToArray(), "rowCount", typeof(long));
        builder.AddColumn(columnCounts.ToArray(), "columnCount", typeof(long));
        return builder.Build();
    }
}
