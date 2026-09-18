using Ara3D.DataTable;

namespace BimOpenFlow.Relations.DuckDb.Tests;

/// <summary>An in-memory table reaching DuckDB through schema "_inline", on its own and
/// joined to the fixture's walls.csv.</summary>
[TestFixture]
public sealed class InlineTableExecuteTests
{
    private Fixture _f = null!;
    [OneTimeSetUp] public void Up() => _f = new Fixture();
    [OneTimeTearDown] public void Down() => _f.Dispose();

    /// <summary>Two rows keyed to walls 1 and 3 of the fixture CSV.</summary>
    static readonly IDataTable Rows =
        NodeTestHelpers.Table(("id", new long[] { 1, 3 }), ("tag", new[] { "keep", "drop" })).Table;

    static readonly Schema RowSchema = new([new("id", ColumnType.Integer), new("tag", ColumnType.Text)]);

    const string Hash = "hash-of-two-rows";

    static InlineTable Plan(string name = "t") => new(name, Hash, RowSchema);

    static InlineTableStore Store()
    {
        var store = new InlineTableStore();
        store.Add(Hash, Rows);
        return store;
    }

    [Test]
    public void MaterializesTheRegisteredRows()
    {
        var table = Plan().Execute(_f.Catalog, _f.Registry, inlines: Store());
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "id", "tag" }));
        Assert.That(table.ColumnCells("id"), Is.EqualTo(new object?[] { 1L, 3L }));
        Assert.That(table.ColumnCells("tag"), Is.EqualTo(new object?[] { "keep", "drop" }));
    }

    [Test]
    public void CountsWithoutMaterializing()
        => Assert.That(Plan().Compile(_f.Catalog).Count(_f.Registry, Store()), Is.EqualTo(2));

    [Test]
    public void FiltersLikeAnyOtherSource()
    {
        var table = Plan().Filter("[tag] == 'keep'").Execute(_f.Catalog, _f.Registry, inlines: Store());
        Assert.That(table.ColumnCells("id"), Is.EqualTo(new object?[] { 1L }));
    }

    [Test]
    public void JoinsToACsvSource()
    {
        var plan = Plan().Join(Fixture.Walls, "id", "id").Select("id", "tag", "height").SortBy("id");
        var table = plan.Execute(_f.Catalog, _f.Registry, inlines: Store());
        Assert.That(table.ColumnCells("tag"), Is.EqualTo(new object?[] { "keep", "drop" }));
        Assert.That(table.ColumnCells("height"), Is.EqualTo(new object?[] { 2.5, 2.1 }));
    }

    [Test]
    public void TwoInlineTablesGetSeparateNames()
    {
        var other = new InlineTable("u", "hash-of-one-row",
            new Schema([new("id", ColumnType.Integer), new("note", ColumnType.Text)]));
        var store = Store();
        store.Add(other.TableHash, NodeTestHelpers.Table(("id", new long[] { 3 }), ("note", new[] { "only" })).Table);
        var table = Plan().Join(other, "id", "id").Select("tag", "note").Execute(_f.Catalog, _f.Registry, inlines: store);
        Assert.That(table.ColumnCells("tag"), Is.EqualTo(new object?[] { "drop" }));
        Assert.That(table.ColumnCells("note"), Is.EqualTo(new object?[] { "only" }));
    }

    [Test]
    public void UnregisteredRowsNameTheTable()
        => Assert.That(() => Plan("walls").Execute(_f.Catalog, _f.Registry, inlines: new InlineTableStore()),
            Throws.ArgumentException.With.Message.Contains("inline table 'walls'"));

    [Test]
    public void NoStoreIsTheSameFailure()
        => Assert.That(() => Plan().Execute(_f.Catalog, _f.Registry),
            Throws.ArgumentException.With.Message.Contains("No rows are registered"));

    [Test]
    public void StoreEvictsTheOldestEntry()
    {
        var store = new InlineTableStore(2);
        store.Add("a", Rows);
        store.Add("b", Rows);
        store.Add("c", Rows);
        Assert.That(store.Count, Is.EqualTo(2));
        Assert.That(store.Find("a"), Is.Null);
        Assert.That(store.Find("c"), Is.SameAs(Rows));
    }

    [Test]
    public void ReRegisteringAHashDoesNotAge()
    {
        var store = new InlineTableStore(2);
        store.Add("a", Rows);
        store.Add("a", Rows);
        store.Add("b", Rows);
        Assert.That(store.Find("a"), Is.SameAs(Rows));
    }
}
