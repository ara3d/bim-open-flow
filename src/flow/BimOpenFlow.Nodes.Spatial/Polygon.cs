using Ara3D.Geometry;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>A plan (XY) polygon kept in double precision, with its predicates and
/// measures delegated to Ara3D.Geometry's polygon operations. The SDK works in single
/// precision, so the polygon is handed over translated to its own bounds minimum:
/// rounding then scales with the polygon's extent, not with site coordinates.</summary>
public sealed class Polygon
{
    public IReadOnlyList<(double X, double Y)> Vertices { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }
    private readonly Polygon2D _local;

    public Polygon(IReadOnlyList<(double X, double Y)> vertices)
    {
        if (vertices.Count < 3)
            throw new ArgumentException("A polygon needs at least three vertices.", nameof(vertices));
        Vertices = vertices;
        MinX = vertices.Min(v => v.X);
        MinY = vertices.Min(v => v.Y);
        MaxX = vertices.Max(v => v.X);
        MaxY = vertices.Max(v => v.Y);
        _local = new Polygon2D(vertices.Select(Local).ToList());
    }

    public static Polygon Rectangle(double minX, double minY, double maxX, double maxY)
        => new([(minX, minY), (maxX, minY), (maxX, maxY), (minX, maxY)]);

    /// <summary>The polygon's plan bounds as a flat box, so the box index can find candidates.</summary>
    public Box Bounds => new(MinX, MinY, 0, MaxX, MaxY, 0);

    public double Area => _local.Area();
    public double Perimeter => _local.Perimeter();
    public int VertexCount => Vertices.Count;
    public bool IsConvex => _local.IsConvex();

    /// <summary>The area-weighted centroid. The SDK's Centroid is the vertex average,
    /// which is not what GIS users expect, so this one is computed here.</summary>
    public (double X, double Y) Centroid
    {
        get
        {
            double twiceArea = 0, cx = 0, cy = 0;
            for (var i = 0; i < Vertices.Count; i++)
            {
                var (x0, y0) = Vertices[i];
                var (x1, y1) = Vertices[(i + 1) % Vertices.Count];
                var cross = x0 * y1 - x1 * y0;
                twiceArea += cross;
                cx += (x0 + x1) * cross;
                cy += (y0 + y1) * cross;
            }
            if (Math.Abs(twiceArea) < 1e-300)
                return (Vertices.Average(v => v.X), Vertices.Average(v => v.Y));
            return (cx / (3 * twiceArea), cy / (3 * twiceArea));
        }
    }

    /// <summary>True when the point is inside or on the boundary.</summary>
    public bool Contains(double x, double y)
        => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY && _local.Contains(Local((x, y)));

    /// <summary>True when the two polygons share any point: an edge of one crosses or
    /// touches an edge of the other, or one lies entirely inside the other.</summary>
    public bool Intersects(Polygon other)
    {
        if (!Bounds.Intersects(other.Bounds)) return false;
        var mine = new Polygon2D(Vertices.Select(other.Local).ToList());
        foreach (var e in mine.Edges())
            foreach (var f in other._local.Edges())
                if (PolygonOps.SegmentsIntersect(e, f))
                    return true;
        var (ox, oy) = other.Vertices[0];
        var (mx, my) = Vertices[0];
        return Contains(ox, oy) || other.Contains(mx, my);
    }

    private Vector2 Local((double X, double Y) v)
        => new((float)(v.X - MinX), (float)(v.Y - MinY));

    public string ToWkt() => Wkt.Polygon(Vertices);
}
