namespace BimOpenFlow.Relations.DuckDb.Tests;

[TestFixture]
public sealed class DuckDbExecutorTests
{
    private Fixture _f = null!;
    [OneTimeSetUp] public void Up() => _f = new Fixture();
    [OneTimeTearDown] public void Down() => _f.Dispose();

    [Test]
    public void FilterJoinAggregateRunsAsOneStatement()
    {
        var plan = Fixture.Walls.Filter("[height] > 2.2")
            .Join(Fixture.Levels, "level_id", "id", JoinKind.Left)
            .Aggregate(["name_right"], new Aggregation(AggregateFunction.Count, null, "n"), new Aggregation(AggregateFunction.Sum, "height", "total"));
        var table = plan.Execute(_f.Catalog, _f.Registry);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name_right", "n", "total" }));
        Assert.That(table.ColumnCells("name_right"), Is.EqualTo(new object?[] { "Ground", null }));
        Assert.That(table.ColumnCells("n"), Is.EqualTo(new object?[] { 2L, 1L }));
        Assert.That(table.ColumnCells("total"), Is.EqualTo(new object?[] { 5.5, 4.0 }));
    }

    [Test]
    public void LimitAndOffsetPage()
    {
        var plan = Fixture.Walls.SortBy("id");
        var page = plan.Execute(_f.Catalog, _f.Registry, limit: 2, offset: 1);
        Assert.That(page.ColumnCells("id"), Is.EqualTo(new object?[] { 2L, 3L }));
    }

    [Test]
    public void CountIsTheUnpagedTotal()
        => Assert.That(Fixture.Walls.Filter("[height] >= 3").Compile(_f.Catalog).Count(_f.Registry), Is.EqualTo(2));

    [Test]
    public void DatesArriveAsIsoText()
    {
        var table = Fixture.Levels.Execute(_f.Catalog, _f.Registry);
        Assert.That(table.ColumnCells("built"), Is.EqualTo(new object?[] { "2020-01-01", "2021-06-30" }));
    }

    [Test]
    public void DeriveAndRawSqlCompose()
    {
        var plan = Plans.Sql("SELECT id, tall FROM t1 WHERE tall", Fixture.Walls.Derive("tall", "[height] > 2.4"));
        var table = plan.Execute(_f.Catalog, _f.Registry);
        Assert.That(table.ColumnCells("id"), Is.EqualTo(new object?[] { 1L, 2L, 4L }));
    }

    [Test]
    public void SharedSubtreeIsOneCte()
    {
        var walls = Fixture.Walls.Filter("[height] > 2.2");
        var query = walls.Join(walls.Limit(1), "id", "id").Compile(_f.Catalog);
        Assert.That(query.Sql.Split("AS (").Length - 1, Is.EqualTo(4));
        Assert.That(query.Execute(_f.Registry).Rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void ResultMustMatchTheInferredSchema()
    {
        var table = Fixture.Walls.Compile(_f.Catalog).Execute(_f.Registry);
        var wrong = new Schema([new("id", ColumnType.Text), new("height", ColumnType.Number), new("level_id", ColumnType.Integer), new("name", ColumnType.Text)]);
        Assert.That(() => table.Conforming(wrong), Throws.InvalidOperationException.With.Message.Contains("inferred as Text"));
    }

    [Test]
    public void ResultCacheKeysOnPlanAndLimitAndIsBounded()
    {
        var cache = new ResultCache(capacity: 2);
        var calls = 0;
        Ara3D.DataTable.IDataTable Run(Plan plan, long? limit)
            => cache.GetOrAdd(plan, limit, 0, () => { calls++; return plan.Execute(_f.Catalog, _f.Registry, limit); });

        Run(Fixture.Walls, 5);
        Run(Fixture.Walls, 5);
        Assert.That(calls, Is.EqualTo(1), "same plan and limit is a hit");
        Run(Fixture.Walls, null);
        Run(Fixture.Walls.Limit(1), null);
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(cache.Count, Is.EqualTo(2), "oldest entry evicted");
    }
}
