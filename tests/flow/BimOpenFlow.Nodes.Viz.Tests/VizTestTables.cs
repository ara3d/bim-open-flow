using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Viz.Tests;

internal static class VizTestTables
{
    /// <summary>name (Text), count (Integer), cost (Number); rows unsorted
    /// on every column.</summary>
    public static TableValue Sample()
        => NodeTestHelpers.Table(
            ("name", typeof(string), ["b", "a", "c"]),
            ("count", typeof(long), [2L, 3L, 1L]),
            ("cost", typeof(double), [1.5, 0.5, 2.5]));
}
