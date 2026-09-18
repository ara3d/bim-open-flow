namespace BimOpenFlow.Relations.Tests;

/// <summary>One test per inference rule against a dictionary catalog; no database.</summary>
public class SchemaInferenceTests
{
    static readonly Schema Walls = new([
        new("id", ColumnType.Integer, false), new("height", ColumnType.Number), new("level_id", ColumnType.Integer), new("name", ColumnType.Text)]);
    static readonly Schema Levels = new([new("id", ColumnType.Integer, false), new("name", ColumnType.Text), new("built", ColumnType.Date)]);

    static readonly ICatalog Catalog = new StaticCatalog(new Dictionary<string, Schema>
    {
        [StaticCatalog.Key("files", "walls.csv")] = Walls,
        [StaticCatalog.Key("db", "levels")] = Levels,
    });

    static Plan WallsPlan => Plans.Csv("files", "walls.csv");
    static Plan LevelsPlan => Plans.Table("db", "levels");

    static string Infer(Plan plan) => plan.Infer(Catalog).ToString();

    [Test] public void Source() => Assert.That(Infer(WallsPlan), Is.EqualTo("id:Integer, height:Number?, level_id:Integer?, name:Text?"));
    [Test] public void UnknownSource() => Assert.That(Infer(Plans.Csv("files", "nope.csv")), Is.EqualTo("Unknown source 'files/nope.csv'."));
    [Test] public void SelectReordersAndKeepsSpelling() => Assert.That(Infer(WallsPlan.Select("HEIGHT", "id")), Is.EqualTo("height:Number?, id:Integer"));
    [Test] public void SelectUnknown() => Assert.That(Infer(WallsPlan.Select("width")), Is.EqualTo("No column named 'width'."));
    [Test] public void Rename() => Assert.That(Infer(WallsPlan.Rename("name", "label").Select("label")), Is.EqualTo("label:Text?"));
    [Test] public void RenameClash() => Assert.That(Infer(WallsPlan.Rename("name", "id")), Is.EqualTo("Rename produces duplicate column 'id'."));
    [Test] public void Cast() => Assert.That(Infer(WallsPlan.Cast("id", ColumnType.Text).Select("id")), Is.EqualTo("id:Text"));
    [Test] public void DeriveTypesTheExpression() => Assert.That(Infer(WallsPlan.Derive("tall", "[height] > 3").Select("tall")), Is.EqualTo("tall:Boolean?"));
    [Test] public void DeriveToNumberIsNullableNumber() => Assert.That(Infer(WallsPlan.Derive("w", "toNumber([name]) * 1000").Select("w")), Is.EqualTo("w:Number?"));
    [Test] public void DeriveToNumberNeedsText() => Assert.That(Infer(WallsPlan.Derive("w", "toNumber([height])")), Does.StartWith("In 'w': ").And.Contain("Text argument"));
    [Test] public void DeriveIntegerArithmetic() => Assert.That(Infer(WallsPlan.Derive("twice", "[id] * 2").Select("twice")), Is.EqualTo("twice:Integer?"));
    [Test] public void DeriveUnknownColumn() => Assert.That(Infer(WallsPlan.Derive("x", "[width] * 2")), Does.StartWith("In 'x': Unknown identifier"));
    [Test] public void DeriveExisting() => Assert.That(Infer(WallsPlan.Derive("id", "1")), Is.EqualTo("Column 'id' already exists."));
    [Test] public void DateColumnsAreInvisibleToExpressions() => Assert.That(Infer(LevelsPlan.Filter("[built] == null")), Does.StartWith("In predicate: Unknown identifier"));
    [Test] public void FilterKeepsSchema() => Assert.That(Infer(WallsPlan.Filter("[height] > 3")), Is.EqualTo(Infer(WallsPlan)));
    [Test] public void FilterMustBeBoolean() => Assert.That(Infer(WallsPlan.Filter("[height] + 3")), Is.EqualTo("Predicate must be Boolean, but it is Number."));
    [Test] public void SortUnknown() => Assert.That(Infer(WallsPlan.SortBy("width")), Is.EqualTo("No column named 'width'."));
    [Test] public void LimitAndDistinctKeepSchema() => Assert.That(Infer(WallsPlan.Distinct().Limit(5)), Is.EqualTo(Infer(WallsPlan)));

    [Test]
    public void JoinConcatenatesAndSuffixesCollisions()
        => Assert.That(Infer(WallsPlan.Join(LevelsPlan, "level_id", "id")),
            Is.EqualTo("id:Integer, height:Number?, level_id:Integer?, name:Text?, id_right:Integer, name_right:Text?, built:Date?"));

    [Test] public void SemiJoinKeepsLeftOnly() => Assert.That(Infer(WallsPlan.Join(LevelsPlan, "level_id", "id", JoinKind.Semi)), Is.EqualTo(Infer(WallsPlan)));
    [Test] public void JoinKeyTypeMismatch() => Assert.That(Infer(WallsPlan.Join(LevelsPlan, "name", "id")), Is.EqualTo("Join keys 'name' (Text) and 'id' (Integer) have incompatible types."));
    [Test] public void JoinNumericKeysUnify() => Assert.That(WallsPlan.Join(LevelsPlan, "height", "id").Infer(Catalog).Ok, Is.True);
    [Test] public void UnionSameShape() => Assert.That(Infer(Plans.Union(WallsPlan, WallsPlan)), Is.EqualTo(Infer(WallsPlan)));
    [Test] public void UnionShapeMismatch() => Assert.That(Infer(Plans.Union(WallsPlan, LevelsPlan)), Is.EqualTo("Union input 2 has 3 columns, expected 4."));

    [Test]
    public void AggregateTypes()
        => Assert.That(Infer(WallsPlan.Aggregate(["level_id"],
                new Aggregation(AggregateFunction.Count, null, "n"), new Aggregation(AggregateFunction.Sum, "height", "total"),
                new Aggregation(AggregateFunction.Avg, "id", "mean"), new Aggregation(AggregateFunction.Max, "name", "last"))),
            Is.EqualTo("level_id:Integer?, n:Integer, total:Number?, mean:Number?, last:Text?"));

    [Test] public void SumOfText() => Assert.That(Infer(WallsPlan.Aggregate([], new Aggregation(AggregateFunction.Sum, "name", "s"))), Is.EqualTo("Cannot apply Sum to 'name' of type Text."));
    [Test] public void RawSqlNeedsADatabaseCatalog() => Assert.That(Infer(Plans.Sql("select 1", WallsPlan)), Does.StartWith("Raw SQL cannot be typed"));

    [Test]
    public void ErrorsStopAtTheFirstFailingInput()
        => Assert.That(Infer(WallsPlan.Select("width").Filter("[nope] > 1").Limit(3)), Is.EqualTo("No column named 'width'."));

    [Test]
    public void CacheInfersEachNodeOnce()
    {
        var cache = new SchemaCache(Catalog);
        var plan = WallsPlan.Filter("[height] > 3");
        cache.Infer(plan);
        cache.Infer(plan.Limit(1));
        Assert.That(cache.Count, Is.EqualTo(3));
    }
}
