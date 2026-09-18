namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class WktTests
{
    [Test]
    public void Parses_A_Closed_Ring_Dropping_The_Closing_Vertex()
    {
        Assert.That(Wkt.TryParsePolygon("POLYGON((0 0, 4 0, 4 3, 0 3, 0 0))", out var p), Is.True);
        Assert.That(p.Outer, Is.EqualTo(new[] { (0.0, 0.0), (4.0, 0.0), (4.0, 3.0), (0.0, 3.0) }));
        Assert.That(p.Holes, Is.EqualTo(0));
    }

    [Test]
    public void Tolerates_Case_Whitespace_Z_Ordinates_And_Holes()
    {
        Assert.That(Wkt.TryParsePolygon("polygon Z ( ( 0 0 5 , 4 0 5 , 4 3 5 ) , ( 1 1 5 , 2 1 5 , 2 2 5 ) )", out var p), Is.True);
        Assert.That(p.Outer.Count, Is.EqualTo(3));
        Assert.That(p.Holes, Is.EqualTo(1));
    }

    [TestCase("POINT(1 2)")]
    [TestCase("POLYGON((0 0, 1 0))")]
    [TestCase("POLYGON((0 0, 1 x, 1 1))")]
    [TestCase("not geometry")]
    public void Rejects_Non_Polygons(string text)
        => Assert.That(Wkt.TryParsePolygon(text, out _), Is.False);

    [Test]
    public void Parses_Points()
    {
        Assert.That(Wkt.TryParsePoint("POINT (1.5 -2)", out var p), Is.True);
        Assert.That(p, Is.EqualTo((1.5, -2.0)));
        Assert.That(Wkt.TryParsePoint("POLYGON((0 0, 1 0, 1 1))", out _), Is.False);
    }

    [Test]
    public void Writes_Closed_Rings_That_Round_Trip()
    {
        var ring = new[] { (0.1, 0.2), (3.0, 0.2), (1.5, 2.75) };
        var text = Wkt.Polygon(ring);
        Assert.That(text, Is.EqualTo("POLYGON((0.1 0.2, 3 0.2, 1.5 2.75, 0.1 0.2))"));
        Assert.That(Wkt.TryParsePolygon(text, out var back), Is.True);
        Assert.That(back.Outer, Is.EqualTo(ring));
    }
}
