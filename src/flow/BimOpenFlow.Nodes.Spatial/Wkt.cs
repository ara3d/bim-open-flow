using System.Globalization;
using System.Text.RegularExpressions;

namespace BimOpenFlow.Nodes.Spatial;

/// <summary>A parsed WKT polygon: the outer ring and any hole rings as XY vertices
/// (closing vertex dropped). The nodes use only the outer ring today and warn about
/// holes; the rings are kept so boolean operations can have them later.</summary>
public sealed record WktPolygon(IReadOnlyList<(double X, double Y)> Outer, IReadOnlyList<IReadOnlyList<(double X, double Y)>> HoleRings)
{
    public int Holes => HoleRings.Count;
}

/// <summary>Reads and writes the two WKT forms the pack exchanges with GIS tools:
/// POLYGON((x y, ...)[, (hole)...]) and POINT(x y). Extra ordinates (Z, M) are
/// ignored. Anything else is an extension point.</summary>
public static partial class Wkt
{
    [GeneratedRegex(@"^\s*POLYGON\s*(?:ZM?|M)?\s*\(\s*(.*)\)\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex PolygonPattern();

    [GeneratedRegex(@"\(([^()]*)\)")]
    private static partial Regex RingPattern();

    [GeneratedRegex(@"^\s*POINT\s*(?:ZM?|M)?\s*\(\s*([^()]*)\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PointPattern();

    public static bool TryParsePolygon(string text, out WktPolygon polygon)
    {
        polygon = null!;
        var match = PolygonPattern().Match(text);
        if (!match.Success) return false;
        var rings = RingPattern().Matches(match.Groups[1].Value);
        if (rings.Count == 0) return false;
        var parsed = rings.Select(r => ParseRing(r.Groups[1].Value)).ToList();
        if (parsed.Any(r => r == null || r.Count < 3)) return false;
        polygon = new(parsed[0]!, parsed.Skip(1).Select(r => r!).ToList());
        return true;
    }

    public static bool TryParsePoint(string text, out (double X, double Y) point)
    {
        point = default;
        var match = PointPattern().Match(text);
        if (!match.Success) return false;
        var parsed = ParseVertex(match.Groups[1].Value);
        if (parsed == null) return false;
        point = parsed.Value;
        return true;
    }

    private static IReadOnlyList<(double X, double Y)>? ParseRing(string text)
    {
        var vertices = new List<(double X, double Y)>();
        foreach (var part in text.Split(','))
        {
            var vertex = ParseVertex(part);
            if (vertex == null) return null;
            vertices.Add(vertex.Value);
        }
        if (vertices.Count > 1 && vertices[0] == vertices[^1])
            vertices.RemoveAt(vertices.Count - 1);
        return vertices;
    }

    private static (double X, double Y)? ParseVertex(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && double.IsFinite(x) && double.IsFinite(y)
            ? (x, y)
            : null;
    }

    /// <summary>POLYGON text of a ring, closed by repeating the first vertex.</summary>
    public static string Polygon(IReadOnlyList<(double X, double Y)> ring)
        => "POLYGON((" + string.Join(", ", ring.Append(ring[0]).Select(Vertex)) + "))";

    public static string Point((double X, double Y) point)
        => "POINT(" + Vertex(point) + ")";

    private static string Vertex((double X, double Y) v)
        => v.X.ToString("R", CultureInfo.InvariantCulture) + " " + v.Y.ToString("R", CultureInfo.InvariantCulture);
}
