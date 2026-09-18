namespace BimOpenFlow.Nodes.Spatial;

/// <summary>The column vocabulary of the pack: the shape columns it reads
/// (matching bim.bounds and the Geometry pack's boxes table, resolved
/// case-insensitively) and the columns its nodes emit.</summary>
public static class SpatialColumns
{
    public const string MinX = "MinX";
    public const string MinY = "MinY";
    public const string MinZ = "MinZ";
    public const string MaxX = "MaxX";
    public const string MaxY = "MaxY";
    public const string MaxZ = "MaxZ";
    public static readonly IReadOnlyList<string> BoxColumns = [MinX, MinY, MinZ, MaxX, MaxY, MaxZ];

    public const string CenterX = "CenterX";
    public const string CenterY = "CenterY";
    public const string CenterZ = "CenterZ";
    public const string Name = "Name";

    public const string A = "A";
    public const string B = "B";
    public const string Distance = "Distance";
    public const string Rank = "Rank";
    public const string OverlapVolume = "OverlapVolume";
    public const string ContainerVolume = "ContainerVolume";

    public const string Footprint = "Footprint";
    public const string FootprintArea = "FootprintArea";
    public const string Perimeter = "Perimeter";
    public const string Area = "Area";
    public const string CentroidX = "CentroidX";
    public const string CentroidY = "CentroidY";
    public const string Vertices = "Vertices";
    public const string IsConvex = "IsConvex";
}
