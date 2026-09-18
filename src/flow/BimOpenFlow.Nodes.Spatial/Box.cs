using Ara3D.Geometry;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>An axis-aligned box in double precision, the exact geometry every
/// box predicate is decided on. A point is a box with Min == Max. Intervals are
/// closed, so touching boxes intersect with zero overlap volume, as in GIS.</summary>
public readonly record struct Box(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    public static Box Point(double x, double y, double z) => new(x, y, z, x, y, z);

    public bool IsValid
        => double.IsFinite(MinX) && double.IsFinite(MinY) && double.IsFinite(MinZ)
        && double.IsFinite(MaxX) && double.IsFinite(MaxY) && double.IsFinite(MaxZ)
        && MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ;

    public double SizeX => MaxX - MinX;
    public double SizeY => MaxY - MinY;
    public double SizeZ => MaxZ - MinZ;
    public double CenterX => MinX / 2 + MaxX / 2;
    public double CenterY => MinY / 2 + MaxY / 2;
    public double CenterZ => MinZ / 2 + MaxZ / 2;
    public double Volume => SizeX * SizeY * SizeZ;
    public double FootprintArea => SizeX * SizeY;

    public bool Intersects(Box o)
        => MinX <= o.MaxX && o.MinX <= MaxX && MinY <= o.MaxY && o.MinY <= MaxY && MinZ <= o.MaxZ && o.MinZ <= MaxZ;

    public double OverlapVolume(Box o)
        => Math.Max(0, Math.Min(MaxX, o.MaxX) - Math.Max(MinX, o.MinX))
        * Math.Max(0, Math.Min(MaxY, o.MaxY) - Math.Max(MinY, o.MinY))
        * Math.Max(0, Math.Min(MaxZ, o.MaxZ) - Math.Max(MinZ, o.MinZ));

    /// <summary>True when this box holds every point of the other; plan-only when ignoreZ.</summary>
    public bool Contains(Box o, bool ignoreZ = false)
        => MinX <= o.MinX && o.MaxX <= MaxX && MinY <= o.MinY && o.MaxY <= MaxY
        && (ignoreZ || (MinZ <= o.MinZ && o.MaxZ <= MaxZ));

    /// <summary>Shortest distance between the two boxes' surfaces; zero when they intersect.</summary>
    public double Distance(Box o)
    {
        var dx = Math.Max(0, Math.Max(MinX - o.MaxX, o.MinX - MaxX));
        var dy = Math.Max(0, Math.Max(MinY - o.MaxY, o.MinY - MaxY));
        var dz = Math.Max(0, Math.Max(MinZ - o.MaxZ, o.MinZ - MaxZ));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public double CenterDistance(Box o)
    {
        var dx = CenterX - o.CenterX;
        var dy = CenterY - o.CenterY;
        var dz = CenterZ - o.CenterZ;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public Box Expand(double amount)
        => new(MinX - amount, MinY - amount, MinZ - amount, MaxX + amount, MaxY + amount, MaxZ + amount);

    /// <summary>The box with its Z extent replaced, for plan-only candidate queries.</summary>
    public Box WithZ(double minZ, double maxZ)
        => this with { MinZ = minZ, MaxZ = maxZ };

    public Box Union(Box o)
        => new(Math.Min(MinX, o.MinX), Math.Min(MinY, o.MinY), Math.Min(MinZ, o.MinZ),
            Math.Max(MaxX, o.MaxX), Math.Max(MaxY, o.MaxY), Math.Max(MaxZ, o.MaxZ));

    /// <summary>Single-precision bounds for the AABB tree, padded outward so float
    /// rounding never drops a true candidate; exact tests run on the Box afterwards.</summary>
    public Bounds3D ToBounds3D()
    {
        var pad = 1e-6 * Math.Max(1, Math.Max(Math.Abs(MinX), Math.Max(Math.Abs(MinY), Math.Max(Math.Abs(MinZ),
            Math.Max(Math.Abs(MaxX), Math.Max(Math.Abs(MaxY), Math.Abs(MaxZ)))))));
        var e = Expand(pad);
        return new Bounds3D(
            new Point3D((float)e.MinX, (float)e.MinY, (float)e.MinZ),
            new Point3D((float)e.MaxX, (float)e.MaxY, (float)e.MaxZ));
    }
}
