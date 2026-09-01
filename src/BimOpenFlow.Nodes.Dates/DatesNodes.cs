using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Dates;

/// <summary>The Dates pack: parse, extract, truncate, arithmetic, and range
/// filtering over ISO-8601 date columns, backed by DuckDB date functions.</summary>
public static class DatesNodes
{
    public static IReadOnlyList<IFlowNode> All { get; } = [];
}
