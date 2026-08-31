using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;
using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

[TestFixture]
public sealed class ConversionTests
{
    private string _root = null!;
    private string _cache = null!;
    private StubConverter _stub = null!;
    private ModelCatalog _catalog = null!;

    [SetUp]
    public void SetUp()
    {
        _root = NewTempDir();
        _cache = NewTempDir();
        _stub = new StubConverter();
        _catalog = new ModelCatalog(_root, _cache, _stub);
    }

    [TearDown]
    public void TearDown()
    {
        DeleteTempDir(_root);
        DeleteTempDir(_cache);
    }

    [Test]
    public void GetBos_BosEntry_ReturnsSourceWithoutConverting()
    {
        var path = WriteFile(_root, "m.bos");
        var entry = _catalog.Scan().Single();

        Assert.That(_catalog.GetBos(entry), Is.EqualTo(path));
        Assert.That(_stub.Calls, Is.Zero);
    }

    [Test]
    public void GetBos_IfcEntry_ConvertsIntoCacheKeyedByContentHash()
    {
        WriteFile(_root, "m.ifc", "ifc content");
        var entry = _catalog.Scan().Single();

        var bos = _catalog.GetBos(entry);

        Assert.That(bos, Is.EqualTo(Path.Combine(_cache, entry.ContentHash + ".bos")));
        Assert.That(File.Exists(bos), Is.True);
        Assert.That(_stub.Calls, Is.EqualTo(1));
    }

    [Test]
    public void GetBos_SecondCall_HitsCache()
    {
        WriteFile(_root, "m.ifc", "ifc content");
        var entry = _catalog.Scan().Single();

        var first = _catalog.GetBos(entry);
        var second = _catalog.GetBos(entry);

        Assert.That(second, Is.EqualTo(first));
        Assert.That(_stub.Calls, Is.EqualTo(1));
    }

    [Test]
    public void GetBos_FreshCatalogInstance_StillHitsCache()
    {
        WriteFile(_root, "m.ifc", "ifc content");
        _catalog.GetBos(_catalog.Scan().Single());

        var otherStub = new StubConverter();
        var other = new ModelCatalog(_root, _cache, otherStub);
        other.GetBos(other.Scan().Single());

        Assert.That(otherStub.Calls, Is.Zero);
    }

    [Test]
    public void GetBos_ChangedSource_ConvertsAgain()
    {
        WriteFile(_root, "m.ifc", "version 1");
        _catalog.GetBos(_catalog.Scan().Single());

        WriteFile(_root, "m.ifc", "version 2");
        _catalog.GetBos(_catalog.Scan().Single());

        Assert.That(_stub.Calls, Is.EqualTo(2));
    }

    [Test]
    public void GetBos_NoTempFilesLeftInCache()
    {
        WriteFile(_root, "m.ifc", "ifc content");
        _catalog.GetBos(_catalog.Scan().Single());

        Assert.That(Directory.GetFiles(_cache, "*.tmp"), Is.Empty);
    }

    [Test]
    public void GetInfo_ReportsTableSizes()
    {
        var bdb = new BimDataBuilder();
        var doc = bdb.AddDocument("TestDoc", "test.ifc");
        var category = bdb.AddEntity(1, "guid-cat", doc, "Walls",
            BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex);
        var wall = bdb.AddEntity(2, "guid-wall", doc, "Wall-001", category, BimDataBuilder.InvalidEntityIndex);
        bdb.AddParameter(wall, "Concrete", "Material", "", "Construction");
        bdb.AddParameter(wall, 2.5, "Height", "m", "Dimensions");
        bdb.AddRelation(wall, category, RelationType.MemberOf);
        bdb.Build().WriteToParquetZip(new FilePath(Path.Combine(_root, "tiny.bos")));

        var info = _catalog.GetInfo(_catalog.Scan().Single());

        Assert.That(info.EntityCount, Is.EqualTo(2));
        Assert.That(info.ParameterCount, Is.EqualTo(2));
        Assert.That(info.DocumentCount, Is.EqualTo(1));
        Assert.That(info.RelationCount, Is.EqualTo(1));
    }
}
