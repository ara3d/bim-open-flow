namespace BimOpenFlow.Relations.Tests;

/// <summary>Compile is text generation, so every test is a string comparison.</summary>
public class SqlCompilerTests
{
    static readonly ICatalog Catalog = new StaticCatalog(new Dictionary<string, Schema>
    {
        [StaticCatalog.Key("files", "walls.csv")] = new([new("id", ColumnType.Integer), new("height", ColumnType.Number), new("level_id", ColumnType.Integer)]),
        [StaticCatalog.Key("db", "levels")] = new([new("id", ColumnType.Integer), new("name", ColumnType.Text)]),
    }, new Dictionary<string, Schema> { [JoinSql] = new([new("id", ColumnType.Integer)]) });

    const string JoinSql = "select t1.id from t1 join t2 on t1.level_id = t2.id";

    static Plan Walls => Plans.Csv("files", "walls.csv");
    static Plan Levels => Plans.Table("db", "levels");

    /// <summary>The body of the last CTE, which is the node under test.</summary>
    static string Last(Plan plan)
    {
        var sql = plan.Compile(Catalog).Sql;
        var start = System.Text.RegularExpressions.Regex.Matches(sql, @"n\d+ AS \(")[^1];
        var end = sql.LastIndexOf(")\nSELECT", StringComparison.Ordinal);
        return sql[(start.Index + start.Length)..end];
    }

    [Test]
    public void WholeStatement()
        => Assert.That(Walls.Filter("[height] > 3").Compile(Catalog).Sql, Is.EqualTo(
            "WITH n1 AS (SELECT * FROM \"files\".\"walls.csv\"),\n"
            + "     n2 AS (SELECT * FROM n1 WHERE (\"height\" > 3))\n"
            + "SELECT * FROM n2"));

    [Test]
    public void SourcesAreListedOnce()
    {
        var query = Walls.Join(Walls.Limit(1), "id", "id").Join(Levels, "level_id", "id").Compile(Catalog);
        Assert.That(query.Sources, Is.EqualTo(new[] { new SourceUse("files", SourceKind.Csv, "walls.csv"), new SourceUse("db", SourceKind.Table, "levels") }));
    }

    [Test] public void Select() => Assert.That(Last(Walls.Select("height", "id")), Is.EqualTo("SELECT \"height\", \"id\" FROM n1"));
    [Test] public void Rename() => Assert.That(Last(Walls.Rename("height", "h")), Is.EqualTo("SELECT \"id\", \"height\" AS \"h\", \"level_id\" FROM n1"));
    [Test] public void Cast() => Assert.That(Last(Walls.Cast("id", ColumnType.Text)), Is.EqualTo("SELECT CAST(\"id\" AS VARCHAR) AS \"id\", \"height\", \"level_id\" FROM n1"));
    [Test] public void Derive() => Assert.That(Last(Walls.Derive("tall", "[height] > 3")), Is.EqualTo("SELECT *, (\"height\" > 3) AS \"tall\" FROM n1"));
    [Test] public void Distinct() => Assert.That(Last(Walls.Distinct()), Is.EqualTo("SELECT DISTINCT * FROM n1"));
    [Test] public void Sort() => Assert.That(Last(Walls.Sort(new("height", true), new("id"))), Is.EqualTo("SELECT * FROM n1 ORDER BY \"height\" DESC, \"id\" ASC"));
    [Test] public void Limit() => Assert.That(Last(Walls.Limit(10, 20)), Is.EqualTo("SELECT * FROM n1 LIMIT 10 OFFSET 20"));
    [Test] public void Union() => Assert.That(Last(Plans.Union(Walls, Walls.Limit(1))), Is.EqualTo("SELECT * FROM n1 UNION ALL SELECT * FROM n2"));

    [Test]
    public void JoinSuffixesCollidingRightColumns()
        => Assert.That(Last(Walls.Join(Levels, "level_id", "id", JoinKind.Left)), Is.EqualTo(
            "SELECT l.\"id\", l.\"height\", l.\"level_id\", r.\"id\" AS \"id_right\", r.\"name\" FROM n1 l LEFT JOIN n2 r ON l.\"level_id\" = r.\"id\""));

    [Test]
    public void AntiJoinKeepsLeft()
        => Assert.That(Last(Walls.Join(Levels, "level_id", "id", JoinKind.Anti)), Is.EqualTo(
            "SELECT l.\"id\", l.\"height\", l.\"level_id\" FROM n1 l ANTI JOIN n2 r ON l.\"level_id\" = r.\"id\""));

    [Test]
    public void AggregateCastsSums()
        => Assert.That(Last(Walls.Aggregate(["level_id"], new Aggregation(AggregateFunction.Count, null, "n"), new Aggregation(AggregateFunction.Sum, "height", "total"), new Aggregation(AggregateFunction.Avg, "id", "mean"))),
            Is.EqualTo("SELECT \"level_id\", count(*) AS \"n\", CAST(sum(\"height\") AS DOUBLE) AS \"total\", CAST(avg(\"id\") AS DOUBLE) AS \"mean\" FROM n1 GROUP BY \"level_id\" ORDER BY \"level_id\""));

    [Test]
    public void AggregateWithoutGroups()
        => Assert.That(Last(Walls.Aggregate([], new Aggregation(AggregateFunction.Max, "height", "top"))), Is.EqualTo("SELECT max(\"height\") AS \"top\" FROM n1"));

    [Test]
    public void RawSqlBindsInputs()
        => Assert.That(Last(Plans.Sql(JoinSql + ";", Walls, Levels)),
            Is.EqualTo("WITH t1 AS (SELECT * FROM n1), t2 AS (SELECT * FROM n2) " + JoinSql));

    [Test]
    public void RawSqlMustBeReadOnly()
        => Assert.Throws<ArgumentException>(() => Plans.Sql("drop table x", Walls));

    [Test]
    public void SchemaErrorsStopCompilation()
        => Assert.Throws<InvalidOperationException>(() => Walls.Join(Levels, "nope", "id").Compile(Catalog));

    [Test]
    public void LimitWrapper()
        => Assert.That(Walls.Compile(Catalog).WithLimit(5, 2), Does.EndWith("SELECT * FROM n1) AS _q LIMIT 5 OFFSET 2"));
}
