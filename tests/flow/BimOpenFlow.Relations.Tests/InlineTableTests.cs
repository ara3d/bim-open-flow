namespace BimOpenFlow.Relations.Tests;

/// <summary>The pure half of the inline-table contract: an InlineTable carries its own
/// schema, renders as (inline "name" "hash"), and compiles to a read of "_inline"."name".</summary>
public class InlineTableTests
{
    static readonly Schema Rows = new([new("id", ColumnType.Integer), new("label", ColumnType.Text)]);
    const string HashA = "aaaa";
    const string HashB = "bbbb";

    static InlineTable Inline(string name = "t", string hash = HashA) => new(name, hash, Rows);

    static readonly ICatalog Catalog = new StaticCatalog(new Dictionary<string, Schema>
    {
        [StaticCatalog.Key("files", "walls.csv")] = new([new("id", ColumnType.Integer), new("height", ColumnType.Number)]),
    });

    static Plan Walls => Plans.Csv("files", "walls.csv");

    [Test]
    public void RendersAsNameAndHash()
        => Assert.That(Inline().Text, Is.EqualTo("(inline \"t\" \"aaaa\")"));

    [Test]
    public void SameTableAndNameIsTheSamePlan()
    {
        Assert.That(Inline().Hash, Is.EqualTo(Inline().Hash));
        Assert.That(Inline(), Is.EqualTo(Inline()));
    }

    [Test]
    public void DifferentRowsAreADifferentPlan()
        => Assert.That(Inline(hash: HashB).Hash, Is.Not.EqualTo(Inline().Hash));

    [Test]
    public void DifferentNameIsADifferentPlan()
        => Assert.That(Inline("u").Hash, Is.Not.EqualTo(Inline().Hash));

    /// <summary>The content hash is not the plan hash; the plan hash is over the rendered text.</summary>
    [Test]
    public void TableHashIsSeparateFromThePlanHash()
        => Assert.That(Inline().TableHash, Is.EqualTo(HashA).And.Not.EqualTo(Inline().Hash));

    [Test]
    public void SchemaIsReturnedAsGiven()
        => Assert.That(Inline().Infer(Catalog).ToString(), Is.EqualTo("id:Integer?, label:Text?"));

    [Test]
    public void SchemaNeedsNoCatalogEntry()
        => Assert.That(Inline().Infer(new StaticCatalog(new Dictionary<string, Schema>())).Ok, Is.True);

    [Test]
    public void CompilesToAReadOfTheInlineSchema()
        => Assert.That(Inline().Compile(Catalog).Sql, Is.EqualTo(
            "WITH n1 AS (SELECT * FROM \"_inline\".\"t\")\nSELECT * FROM n1"));

    [Test]
    public void CompiledQueryListsTheInlineTable()
    {
        var query = Inline().Compile(Catalog);
        Assert.That(query.Inlines, Is.EqualTo(new[] { new InlineUse("t", HashA) }));
        Assert.That(query.Sources, Is.Empty);
    }

    [Test]
    public void EachInlineTableIsListedOnce()
    {
        var query = Inline().Join(Inline().Limit(1), "id", "id").Compile(Catalog);
        Assert.That(query.Inlines, Is.EqualTo(new[] { new InlineUse("t", HashA) }));
    }

    [Test]
    public void TwoInlineTablesAreBothListed()
    {
        var query = Inline("t").Join(Inline("u", HashB), "id", "id").Compile(Catalog);
        Assert.That(query.Inlines, Is.EqualTo(new[] { new InlineUse("t", HashA), new InlineUse("u", HashB) }));
    }

    [Test]
    public void ComposesUnderFilterAndJoin()
    {
        var plan = Inline().Filter("[id] > 1").Join(Walls, "id", "id");
        Assert.That(plan.Infer(Catalog).ToString(), Is.EqualTo("id:Integer?, label:Text?, id_right:Integer?, height:Number?"));
        var query = plan.Compile(Catalog);
        Assert.That(query.Sources, Is.EqualTo(new[] { new SourceUse("files", SourceKind.Csv, "walls.csv") }));
        Assert.That(query.Inlines, Is.EqualTo(new[] { new InlineUse("t", HashA) }));
        Assert.That(query.Sql, Is.EqualTo(
            "WITH n1 AS (SELECT * FROM \"_inline\".\"t\"),\n"
            + "     n2 AS (SELECT * FROM n1 WHERE (\"id\" > 1)),\n"
            + "     n3 AS (SELECT * FROM \"files\".\"walls.csv\"),\n"
            + "     n4 AS (SELECT l.\"id\", l.\"label\", r.\"id\" AS \"id_right\", r.\"height\" FROM n2 l JOIN n3 r ON l.\"id\" = r.\"id\")\n"
            + "SELECT * FROM n4"));
    }
}
