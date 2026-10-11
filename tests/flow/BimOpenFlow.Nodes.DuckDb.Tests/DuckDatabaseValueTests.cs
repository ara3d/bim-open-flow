using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

/// <summary>duck.source emits a Database value, and duck.query, duck.table and duck.tables use it
/// when wired into their bindable path parameter, falling back to the typed path when not.</summary>
[TestFixture]
public sealed class DuckDatabaseValueTests
{
    private string folder = null!;
    private string first = null!;
    private string second = null!;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        first = Create("first.duckdb", "walls", 2);
        second = Create("second.duckdb", "doors", 3);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
    }

    private string Create(string name, string table, int rows)
    {
        var path = Path.Combine(folder, name);
        using var conn = new DuckDBConnection($"DataSource={path}");
        conn.Open();
        conn.Execute($"CREATE TABLE {table} (v INTEGER); INSERT INTO {table} SELECT range FROM range({rows})");
        return path;
    }

    private static DatabaseValue Source(DuckSourceCache cache, string path)
        => (DatabaseValue)new DuckSourceNode(cache).Eval(NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(("path", path)))[0];

    [Test]
    public void SourceEmitsADatabaseValueIdentifiedByTheFileContent()
    {
        using var cache = new DuckSourceCache();
        var value = Source(cache, first);
        Assert.That(value.Path, Is.EqualTo(Path.GetFullPath(first)));
        Assert.That(value.Hash, Has.Length.EqualTo(64));
        Assert.That(Source(cache, first), Is.EqualTo(value));
        Assert.That(Source(cache, second), Is.Not.EqualTo(value));
        Assert.That(new DuckSourceNode(cache).Spec.Outputs[0].Type, Is.EqualTo(PortType.Database));
    }

    [Test]
    public void AChangedFileIsANewValue()
    {
        DatabaseValue before;
        using (var firstCache = new DuckSourceCache())
            before = Source(firstCache, first);
        using (var conn = new DuckDBConnection($"DataSource={first}"))
        {
            conn.Open();
            conn.Execute("INSERT INTO walls VALUES (99)");
        }
        using var cache = new DuckSourceCache();
        Assert.That(Source(cache, first).Hash, Is.Not.EqualTo(before.Hash));
    }

    [Test]
    public void TablesAndTableReadFromTheWiredSource()
    {
        using var cache = new DuckSourceCache();
        var source = Source(cache, second);
        var (tables, warnings) = new DuckTablesNode().EvalWithWarnings(source);
        Assert.That(tables.Cell("name", 0), Is.EqualTo("doors"));
        Assert.That(warnings, Is.Empty);
        Assert.That(new DuckTableNode().EvalTable(source, ("table", "doors")).Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void WiredSourceWinsOverTheTypedPath()
    {
        using var cache = new DuckSourceCache();
        var source = Source(cache, second);
        var (table, warnings) = new DuckTableNode().EvalWithWarnings(source, ("path", first), ("table", "doors"));
        Assert.That(table.Rows, Has.Count.EqualTo(3));
        Assert.That(warnings, Is.Empty);

        var query = new DuckQueryNode(cache).EvalTable(source, ("path", first), ("sql", "SELECT * FROM doors"));
        Assert.That(query.Rows, Has.Count.EqualTo(3));
    }

    [Test]
    public void ThePathParameterIsBindableToADatabaseAndTheNodesHaveNoSourceInput()
    {
        foreach (IFlowNode node in new IFlowNode[] { new DuckQueryNode(), new DuckTableNode(), new DuckTablesNode() })
        {
            var path = node.Spec.Params.Single(p => p.Name == "path");
            Assert.That(path.Bindable, Is.EqualTo(PortType.Database), node.Spec.Kind);
            Assert.That(path.Kind, Is.EqualTo(ParamKind.FilePath), node.Spec.Kind);
            Assert.That(node.Spec.Inputs, Is.Empty, node.Spec.Kind);
        }
    }

    [Test]
    public void SourcePayloadListsTheTablesLikeDuckTables()
    {
        using var cache = new DuckSourceCache();
        var source = Source(cache, second);
        var listed = ((Func<Ara3D.DataTable.IDataTable>)source.Payload!)();
        var tables = new DuckTablesNode().EvalTable(source);
        Assert.That(listed.ColumnNames(), Is.EqualTo(tables.ColumnNames()));
        Assert.That(listed.Cell("name", 0), Is.EqualTo("doors"));
        Assert.That(listed.Cell("rowCount", 0), Is.EqualTo(3L));
    }

    [Test]
    public void WithoutASourceThePathParameterIsUsedSilently()
    {
        var (table, warnings) = new DuckTableNode().EvalWithWarnings([], ("path", first), ("table", "walls"));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void WithNeitherSourceNorPathTheNodeSaysSo()
        => Assert.That(() => new DuckTablesNode().EvalTable([]), Throws.Exception.Message.Contain("path"));
}
