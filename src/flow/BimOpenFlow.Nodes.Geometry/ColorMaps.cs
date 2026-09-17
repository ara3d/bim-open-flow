namespace BimOpenFlow.Nodes.Geometry;

public readonly record struct Rgb(double R, double G, double B);

/// <summary>Self-contained colormap math: gradients (viridis, redgreen) and a categorical palette (category10).</summary>
public static class ColorMaps
{
    public static readonly IReadOnlyList<Rgb> ViridisStops =
    [
        new(0.267004, 0.004874, 0.329415),
        new(0.229739, 0.322361, 0.545706),
        new(0.127568, 0.566949, 0.550556),
        new(0.369214, 0.788888, 0.382914),
        new(0.993248, 0.906157, 0.143936),
    ];

    public static readonly IReadOnlyList<Rgb> RedGreenStops =
    [
        new(0.839216, 0.152941, 0.156863),
        new(0.949020, 0.866667, 0.360784),
        new(0.172549, 0.627451, 0.172549),
    ];

    public static readonly IReadOnlyList<Rgb> Category10 =
    [
        FromHex(0x1f77b4), FromHex(0xff7f0e), FromHex(0x2ca02c), FromHex(0xd62728), FromHex(0x9467bd),
        FromHex(0x8c564b), FromHex(0xe377c2), FromHex(0x7f7f7f), FromHex(0xbcbd22), FromHex(0x17becf),
    ];

    public static Rgb FromHex(int rgb)
        => new(((rgb >> 16) & 0xFF) / 255.0, ((rgb >> 8) & 0xFF) / 255.0, (rgb & 0xFF) / 255.0);

    /// <summary>Piecewise-linear sample of evenly spaced stops at t in [0,1]; t is clamped, NaN maps to 0.</summary>
    public static Rgb Gradient(IReadOnlyList<Rgb> stops, double t)
    {
        t = double.IsNaN(t) ? 0 : Math.Clamp(t, 0, 1);
        var scaled = t * (stops.Count - 1);
        var lo = (int)Math.Floor(scaled);
        if (lo >= stops.Count - 1)
            return stops[^1];
        var f = scaled - lo;
        var a = stops[lo];
        var b = stops[lo + 1];
        return new(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
    }

    public static Rgb Viridis(double t)
        => Gradient(ViridisStops, t);

    public static Rgb RedGreen(double t)
        => Gradient(RedGreenStops, t);

    public static Rgb Categorical(int index)
        => Category10[((index % 10) + 10) % 10];
}
