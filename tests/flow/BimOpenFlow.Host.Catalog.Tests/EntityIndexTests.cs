using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>Reads the committed enriched Duplex BOS once for the whole fixture.</summary>
[TestFixture]
public sealed class EntityIndexTests
{
    private const string Sample = "nrc/duplex-enriched.bos";
    private const string WallCategory = "IFCWALLSTANDARDCASE";
    private const string CarbonGroup = "Pset_NRCOperationalCarbon";

    private string _root = "";
    private string _cache = "";
    private ModelCatalog _catalog = null!;
    private ModelEntry _entry = null!;

    [OneTimeSetUp]
    public void Convert()
    {
        var source = FindSample(Sample);
        _root = NewTempDir();
        _cache = NewTempDir();
        File.Copy(source, Path.Combine(_root, Path.GetFileName(source)));
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
    public void EnrichedDuplex_WallCarriesTheNrcOperationalCarbonPset()
    {
        var wall = Walls.FirstOrDefault(w => w.Parameters.Any(p => p.Group == CarbonGroup));
        Assert.That(wall, Is.Not.Null, $"no {WallCategory} carries {CarbonGroup}");
        Assert.That(wall!.GlobalId, Is.Not.Null);
        var carbon = wall.Parameters.Single(p =>
            p.Group == CarbonGroup && p.Name == "OperationalCarbon_kgCO2e_per_year");
        Assert.That(carbon.Value, Is.Not.Empty);
    }

    [Test]
    public void Index_CoversTheWholeModelAndFindsByLocalId()
    {
        Assert.That(Index.Count, Is.GreaterThan(100));
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
