using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>Box to plan polygon: appends a WKT rectangle column, the box's XY
/// footprint, so box tables can flow into the polygon nodes and out to GIS tools.</summary>
public sealed class FootprintNode : IFlowNode
{
    public const string Kind = "spatial.footprint";
    public const string As = "as";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("boxes", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new(As, ParamKind.Text, SpatialColumns.Footprint)],
        "Adds a Text column ('as', default Footprint) holding the WKT POLYGON of each row's "
        + "MinX..MaxY rectangle; rows with a missing bound get null. The column feeds "
        + "spatial.polygon, spatial.polygonContains, and spatial.polygonIntersects, and reads "
        + "unchanged in DuckDB spatial, PostGIS, or QGIS. Errors if the column already exists.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var boxes = inputs.TableInput(0, Kind);
        if (!Shapes.HasBoxColumns(boxes))
            throw new ArgumentException($"{Kind}: input 'boxes' must have the box columns MinX..MaxZ.");
        var asName = parameters.TextOr(As, SpatialColumns.Footprint);
        if (boxes.ColumnIndex(asName) >= 0)
            throw new ArgumentException($"{Kind}: input already has a column named '{asName}'.");
        var side = Shapes.Read(boxes, Kind, boxes.Columns[0].Descriptor.Name, parameters, "boxes");
        var wkt = side.Boxes
            .Select(b => b is { } box ? Polygon.Rectangle(box.MinX, box.MinY, box.MaxX, box.MaxY).ToWkt() : null)
            .ToArray();
        var builder = boxes.CopyBuilder();
        builder.AddColumn(wkt, asName, typeof(string));
        return [new TableValue(builder.Build())];
    }
}
