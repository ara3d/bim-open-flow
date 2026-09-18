using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.Utils;

namespace BimOpenFlow.Nodes.Relations.Tests;

/// <summary>Single-node evaluations over a temp folder: walls.csv under source "files"
/// and levels.duckdb under source "levels" (both derived from the folder by FromRoots).</summary>
[TestFixture]
public sealed class RelationNodeTests
{
    private string _folder = null!;
    private RelationRuntime _runtime = null!;

    [OneTimeSetUp]
    public void Up()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-relnodes-tests", Guid.NewGuid().ToString("N"), "files");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "walls.csv"), "id,height,level_id,name\n1,2.5,10,A\n2,3.0,10,B\n3,2.1,20,C\n4,4.0,30,D\n");
        using (var conn = BosDuckDb.Open(new FilePath(Path.Combine(_folder, "levels.duckdb"))))
        {
            conn.Execute("CREATE TABLE levels (id BIGINT, name VARCHAR)");
            conn.Execute("INSERT INTO levels VALUES (10, 'Ground'), (20, 'First')");
        }
        _runtime = RelationRuntime.FromRoots([_folder]);
    }

    [OneTimeTearDown]
    public void Down()
    {
        try { Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true); }
        catch (IOException) { }
    }

    private RelationValue Rel(IFlowNode node, IReadOnlyList<FlowValue> inputs, params (string, string)[] ps)
        => (RelationValue)node.Eval(NodeTestHelpers.Ctx, inputs, NodeTestHelpers.Params(ps))[0];

    private RelationValue Walls()
        => Rel(new RelCsvNode(_runtime), [], ("source", "files"), ("path", "walls.csv"));

    private RelationValue Levels()
        => Rel(new RelTableNode(_runtime), [], ("source", "levels"), ("table", "levels"));

    [Test]
    public void RootsRegisterTheFolderAndItsDatabases()
        => Assert.That(((RootScanRegistry)_runtime.Registry).Names, Is.EquivalentTo(new[] { "files", "levels" }));

    [Test]
    public void SourceNodesCarryPlansNotRows()
    {
        var walls = Walls();
        Assert.That(walls.Text, Is.EqualTo("(csv \"files\" \"walls.csv\")"));
        Assert.That(walls.Payload, Is.InstanceOf<Plan>());
        Assert.That(_runtime.Schema((Plan)walls.Payload!).ToString(), Is.EqualTo("id:Integer?, height:Number?, level_id:Integer?, name:Text?"));
    }

    [Test]
    public void MissingColumnIsANodeError()
        => Assert.That(() => Rel(new RelSelectNode(_runtime), [Walls()], ("columns", "id, width")),
            Throws.ArgumentException.With.Message.EqualTo("rel.select: No column named 'width'."));

    [Test]
    public void FilterMustBeBoolean()
        => Assert.That(() => Rel(new RelFilterNode(_runtime), [Walls()], ("expr", "[height] + 1")),
            Throws.ArgumentException.With.Message.Contains("must be Boolean"));

    [Test]
    public void ChainMaterializesAsOneQuery()
    {
        var filtered = Rel(new RelFilterNode(_runtime), [Walls()], ("expr", "[height] > 2.2"));
        var joined = Rel(new RelJoinNode(_runtime), [filtered, Levels()], ("leftKey", "level_id"), ("rightKey", "id"), ("kind", "left"));
        var grouped = Rel(new RelAggregateNode(_runtime), [joined], ("groupBy", "name_right"), ("aggregates", "count(*) as n, sum(height) as total"));
        var sorted = Rel(new RelSortNode(_runtime), [grouped], ("by", "n desc, name_right"));
        var table = new RelMaterializeNode(_runtime).EvalTable([sorted]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name_right", "n", "total" }));
        Assert.That(table.ColumnCells("n"), Is.EqualTo(new object?[] { 2L, 1L }));
        Assert.That(table.ColumnCells("name_right"), Is.EqualTo(new object?[] { "Ground", null }));
        Assert.That(_runtime.Compile((Plan)sorted.Payload!).Sql.Split("AS (").Length - 1, Is.EqualTo(6));
    }

    [Test]
    public void DeriveLimitAndSqlCompose()
    {
        var derived = Rel(new RelDeriveNode(_runtime), [Walls()], ("name", "tall"), ("expr", "[height] > 2.4"));
        var sql = Rel(new RelSqlNode(_runtime), [derived], ("sql", "SELECT id FROM t1 WHERE tall ORDER BY id"));
        var limited = Rel(new RelLimitNode(_runtime), [sql], ("count", "2"), ("offset", "1"));
        Assert.That(_runtime.Schema((Plan)limited.Payload!).ToString(), Is.EqualTo("id:Integer?"));
        Assert.That(new RelMaterializeNode(_runtime).EvalTable([limited]).ColumnCells("id"), Is.EqualTo(new object?[] { 2L, 4L }));
    }

    [Test]
    public void MaterializeLimitAndRuntimeCount()
    {
        var table = new RelMaterializeNode(_runtime).EvalTable([Walls()], ("limit", "3"));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(_runtime.Count((Plan)Walls().Payload!), Is.EqualTo(4));
    }

    [Test]
    public void SamePlanHashesTheSameAcrossEvaluations()
        => Assert.That(Ara3D.DataFlowEngine.ValueHash.Compute(Walls()), Is.EqualTo(Ara3D.DataFlowEngine.ValueHash.Compute(Walls())));

    [Test]
    public void BadSqlIsANodeError()
        => Assert.That(() => Rel(new RelSqlNode(_runtime), [Walls()], ("sql", "DROP TABLE t1")),
            Throws.ArgumentException.With.Message.StartWith("rel.sql:"));
}
