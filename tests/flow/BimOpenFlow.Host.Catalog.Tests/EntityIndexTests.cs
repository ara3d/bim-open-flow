using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>Reads a synthetic BOS archive (BimOpenFlow.TestSupport.SampleBos) once for the whole fixture.</summary>
[TestFixture]
public sealed class EntityIndexTests
{
    private const string WallCategory = SampleBos.WallCategory;
    private const string CarbonGroup = SampleBos.CarbonGroup;

    private string _root = "";
    private string _cache = "";
    private ModelCatalog _catalog = null!;
    private ModelEntry _entry = null!;

    [OneTimeSetUp]
    public void Convert()
    {
        _root = NewTempDir();
        _cache = NewTempDir();
        SampleBos.Write(Path.Combine(_root, "sample.bos"));
        _catalog = new(_root, _cache);
        _entry = _catalog.Scan().Single();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        DeleteTempDir(_root);
        DeleteTempDir(_cache);
    }

    private ModelEntityIndex Index => _catalog.GetEntityIndex(_entry);

    private IEnumerable<ModelEntity> Walls
        => Index.All.Where(e => e.Category == WallCategory);

    [Test]
    public void Wall_CarriesTheOperationalCarbonPset()
    {
        var wall = Walls.FirstOrDefault(w => w.Parameters.Any(p => p.Group == CarbonGroup));
        Assert.That(wall, Is.Not.Null, $"no {WallCategory} carries {CarbonGroup}");
        Assert.That(wall!.GlobalId, Is.Not.Null);
        var carbon = wall.Parameters.Single(p =>
            p.Group == CarbonGroup && p.Name == SampleBos.CarbonName);
        Assert.That(carbon.Value, Is.Not.Empty);
    }

    [Test]
    public void Index_CoversTheWholeModelAndFindsByLocalId()
    {
        Assert.That(Index.Count, Is.EqualTo(SampleBos.WallCount + 1));
        var wall = Walls.First();
        Assert.That(Index.Find(wall.LocalId), Is.SameAs(wall));
        Assert.That(Index.Find(long.MaxValue), Is.Null);
    }

    [Test]
    public void Parameters_AreSortedByGroupThenName()
    {
        var keys = Walls.First(w => w.Parameters.Count > 2)
            .Parameters.Select(p => (p.Group, p.Name)).ToList();
        Assert.That(keys, Is.EqualTo(keys
            .OrderBy(k => k.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(k => k.Name, StringComparer.OrdinalIgnoreCase)
            .ToList()));
    }

    [Test]
    public void SecondCall_ReturnsTheCachedIndexInstance()
    {
        var first = _catalog.GetEntityIndex(_entry);
        Assert.That(_catalog.GetEntityIndex(_entry), Is.SameAs(first));
    }
}
