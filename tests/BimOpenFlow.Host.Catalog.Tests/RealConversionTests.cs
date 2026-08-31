using static BimOpenFlow.Host.Catalog.Tests.CatalogTestHelpers;

namespace BimOpenFlow.Host.Catalog.Tests;

[TestFixture]
public sealed class RealConversionTests
{
    [Test]
    [Category("RequiresData")]
    public void DuplexIfc_ConvertsToBosWithEntities()
    {
        var source = FindData("duplex.ifc");
        var root = NewTempDir();
        var cache = NewTempDir();
        try
        {
            File.Copy(source, Path.Combine(root, "duplex.ifc"));
            var catalog = new ModelCatalog(root, cache);
            var entry = catalog.Scan().Single();

            var bos = catalog.GetBos(entry);
            Assert.That(File.Exists(bos), Is.True);
            Assert.That(new FileInfo(bos).Length, Is.GreaterThan(0));

            var info = catalog.GetInfo(entry);
            Assert.That(info.EntityCount, Is.GreaterThan(100));
            Assert.That(info.ParameterCount, Is.GreaterThan(100));
            Assert.That(info.DocumentCount, Is.EqualTo(1));
        }
        finally
        {
            DeleteTempDir(root);
            DeleteTempDir(cache);
        }
    }
}
