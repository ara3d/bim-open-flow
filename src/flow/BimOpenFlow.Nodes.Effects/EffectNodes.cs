using Ara3D.DataFlowEngine.Abstractions;
using System.Linq;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>The effects node pack: every Run-gated sink.</summary>
public static class EffectNodes
{
    /// <summary>The pure-table writers — the sinks a tables-only host serves.</summary>
    public static IReadOnlyList<IFlowNode> TableSinks { get; } =
        new IFlowNode[]
        {
            new ExportCsvNode(),
            new ExportParquetNode(),
            new ExportJsonNode(),
            new ExportXlsxNode(),
            new ExportSqliteNode(),
            new ExportDuckDbNode(),
        };

    /// <summary>Every sink, the table writers plus the BIM-specific ones.</summary>
    public static IReadOnlyList<IFlowNode> All { get; } =
        TableSinks.Concat(new IFlowNode[]
        {
            new WritePsetsNode(),
            new ReportNode(),
        }).ToArray();
}
