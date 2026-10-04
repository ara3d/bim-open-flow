namespace BimOpenFlow.Nodes.Spatial.Tests;

[TestFixture]
public class BoxTests
{
    private static readonly Box Unit = new(0, 0, 0, 1, 1, 1);

    [Test]
    public void Distance_Is_Zero_When_Intersecting_And_Surface_To_Surface_Otherwise()
    {
        Assert.That(Unit.Distance(new Box(0.5, 0.5, 0.5, 2, 2, 2)), Is.EqualTo(0));
        Assert.That(Unit.Distance(new Box(4, 0, 0, 5, 1, 1)), Is.EqualTo(3));
        Assert.That(Unit.Distance(new Box(4, 5, 0, 5, 6, 1)), Is.EqualTo(5));
    }

    [Test]
    public void Center_Distance_Ignores_Extent()
        => Assert.That(Unit.CenterDistance(new Box(3, 0, 0, 4, 1, 1)), Is.EqualTo(3));

    [Test]
    public void Contains_Requires_Full_Inclusion_Unless_IgnoreZ()
    {
        var tall = new Box(0.2, 0.2, -5, 0.8, 0.8, 5);
        Assert.That(Unit.Contains(tall), Is.False);
        Assert.That(Unit.Contains(tall, ignoreZ: true), Is.True);
        Assert.That(Unit.Contains(Box.Point(1, 1, 1)), Is.True);
        Assert.That(Unit.Contains(Box.Point(1.01, 1, 1)), Is.False);
    }

    [Test]
    public void Invalid_Boxes_Are_Detected()
    {
        Assert.That(new Box(1, 0, 0, 0, 1, 1).IsValid, Is.False);
        Assert.That(new Box(0, 0, 0, double.NaN, 1, 1).IsValid, Is.False);
        Assert.That(Box.Point(1, 2, 3).IsValid, Is.True);
    }

    [Test]
    public void Tree_Bounds_Pad_Outward()
    {
        var bounds = new Box(0.1, 0.2, 0.3, 0.4, 0.5, 0.6).ToBounds3D();
        Assert.That((double)bounds.Min.X, Is.LessThan(0.1));
        Assert.That((double)bounds.Max.Z, Is.GreaterThan(0.6));
    }
}
