using Ara3D.DataFlowEngine.Expressions;
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

    /// <summary>isnull, in, and not in keep the same rows in DuckDB as the expression
    /// evaluator does in memory (table.filter), including around a null cell.</summary>
    [TestCase("isnull([tag])", new long[] { 2 })]
    [TestCase("not isnull([tag])", new long[] { 1, 3 })]
    [TestCase("[tag] in ('keep', 'drop')", new long[] { 1, 3 })]
    [TestCase("[tag] not in ('keep')", new long[] { 3 })]
    [TestCase("[id] in (1, 2.0, -7)", new long[] { 1, 2 })]
    [TestCase("[id] not in (2) and not isnull([tag])", new long[] { 1, 3 })]
    public void MissingAndMembershipMatchTheEvaluator(string expr, long[] expected)
    {
        var ids = new long[] { 1, 2, 3 };
        var tags = new[] { "keep", null, "drop" };
        var rows = NodeTestHelpers.Table(("id", typeof(long), ids.Cast<object?>().ToArray()),
            ("tag", typeof(string), tags.Cast<object?>().ToArray())).Table;
        var store = new InlineTableStore();
        store.Add("hash-of-three-rows", rows);
        var plan = new InlineTable("t", "hash-of-three-rows", RowSchema).Filter(expr).SortBy("id");
        var fromDuckDb = plan.Execute(_f.Catalog, _f.Registry, inlines: store).ColumnCells("id");

        var check = Expression.Parse(expr).Check(
            new Dictionary<string, ScalarType>
            {
                ["id"] = ScalarType.Integer,
                ["tag"] = ScalarType.Text,
            });
        var inMemory = ids.Zip(tags)
            .Where(r => check.Eval(name => name == "id"
                    ? new IntegerScalar(r.First)
                    : r.Second is { } t ? new TextScalar(t) : null)
                is BooleanScalar { Value: true })
            .Select(r => (object?)r.First);

        Assert.That(fromDuckDb, Is.EqualTo(expected.Cast<object?>()));
        Assert.That(inMemory, Is.EqualTo(expected.Cast<object?>()));
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
