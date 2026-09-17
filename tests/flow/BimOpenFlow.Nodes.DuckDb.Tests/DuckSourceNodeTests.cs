using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class DuckSourceNodeTests
{
    [Test]
    public void BranchesAndSqlEditsReuseOneReadOnlyConnection()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".duckdb");
        try
        {
            using (var setup = new DuckDBConnection($"DataSource={path}"))
            {
                setup.Open();
                setup.Execute("CREATE TABLE items (name VARCHAR, value INTEGER); INSERT INTO items VALUES ('a', 1), ('b', 2)");
            }
            using var cache = new DuckSourceCache();
            var source = new DuckSourceNode(cache).Eval(NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(("path", path)))[0];
            var query = new DuckQueryNode(cache);
            Assert.That(query.EvalTable([source], ("sql", "SELECT name FROM items")).Rows, Has.Count.EqualTo(2));
            Assert.That(query.EvalTable([source], ("sql", "SELECT sum(value)::BIGINT AS total FROM items")).Cell("total", 0), Is.EqualTo(3L));
            Assert.That(query.EvalTable([source], ("sql", "SELECT value AS Renamed FROM items")).ColumnNames(), Is.EqualTo(new[] { "Renamed" }));
            Assert.That(cache.OpenedConnections, Is.EqualTo(1));
            Assert.That(() => query.EvalTable([source], ("sql", "DELETE FROM items")), Throws.ArgumentException);
            Assert.That(query.EvalTable([source], ("sql", "SELECT * FROM items")).Rows, Has.Count.EqualTo(2));
        }
        finally { File.Delete(path); }
    }

    [Test]
    public void EvictionAndDisposalReleaseDatabaseConnections()
    {
        var paths = Enumerable.Range(0, 2).Select(_ => Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".duckdb")).ToArray();
        try
        {
            foreach (var path in paths) { using var setup = new DuckDBConnection($"DataSource={path}"); setup.Open(); setup.Execute("CREATE TABLE t (v INTEGER)"); }
            using var cache = new DuckSourceCache(1);
            cache.Load(paths[0]); cache.Load(paths[1]); cache.Load(paths[0]);
            Assert.That(cache.OpenedConnections, Is.EqualTo(3));
            cache.Dispose();
            Assert.That(() => cache.Load(paths[0]), Throws.TypeOf<ObjectDisposedException>());
        }
        finally { foreach (var path in paths) File.Delete(path); }
    }
}
