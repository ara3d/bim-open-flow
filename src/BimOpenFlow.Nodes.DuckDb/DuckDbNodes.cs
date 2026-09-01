using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>The DuckDB/SQL node pack: file readers backed by DuckDB and SQL over
/// flowing tables. BIM-free; every value is a plain table.</summary>
public static class DuckDbNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } =
    [
        new DuckReadNode(),
        new DuckQueryNode(),
        new SqlQueryNode(),
    ];
}
