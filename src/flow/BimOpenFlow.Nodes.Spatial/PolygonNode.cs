using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Polygon measures: area, perimeter, centroid, vertex count, and convexity
/// of a WKT polygon column, appended to the table.</summary>
public sealed class PolygonNode : IFlowNode
{
    public const string Kind = "spatial.polygon";

    private static readonly string[] Added =
    [
        SpatialColumns.Area, SpatialColumns.Perimeter, SpatialColumns.CentroidX, SpatialColumns.CentroidY,
        SpatialColumns.Vertices, SpatialColumns.IsConvex,
    ];

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [Shapes.PolygonColumnParam("table")],
        "Adds Area, Perimeter, CentroidX, CentroidY (area-weighted), Vertices, and IsConvex "
        + "computed from the WKT POLYGON in the 'polygon' column (default Footprint); rows "
        + "with an empty cell get nulls, unparsable text is an error naming the row, and holes "
        + "are ignored with a warning. Errors if any added column already exists.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        foreach (var name in Added)
            if (table.ColumnIndex(name) >= 0)
                throw new ArgumentException($"{Kind}: input already has a column named '{name}'.");
        var polygons = Shapes.ParsePolygons(table, Kind,
            parameters.TextOr(Shapes.PolygonParam, SpatialColumns.Footprint), "table", context);

        var builder = table.CopyBuilder();
        object?[] Column<T>(Func<Polygon, T> measure) where T : struct
            => polygons.Select(p => p is { } polygon ? (object?)measure(polygon) : null).ToArray();
        builder.AddColumn(Column(p => p.Area), SpatialColumns.Area, typeof(double));
        builder.AddColumn(Column(p => p.Perimeter), SpatialColumns.Perimeter, typeof(double));
        builder.AddColumn(Column(p => p.Centroid.X), SpatialColumns.CentroidX, typeof(double));
        builder.AddColumn(Column(p => p.Centroid.Y), SpatialColumns.CentroidY, typeof(double));
        builder.AddColumn(Column(p => (long)p.VertexCount), SpatialColumns.Vertices, typeof(long));
        builder.AddColumn(Column(p => p.IsConvex), SpatialColumns.IsConvex, typeof(bool));
        return [new TableValue(builder.Build())];
    }
}
