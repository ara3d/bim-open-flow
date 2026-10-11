namespace BimOpenFlow.Relations.Tests;

/// <summary>Plan identity: canonical text, structural equality, hashing, and traversal.</summary>
public class PlanTests
{
    static Plan Walls()
        => Plans.Csv("files", "walls.csv").Filter("[height] > 3").Select("id", "height");

    [Test]
    public void EqualStructuresAreEqualAndHashTheSame()
    {
        var a = Walls();
        var b = Walls();
        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.Hash, Is.EqualTo(b.Hash));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void DifferentPredicatesDiffer()
    {
        var a = Plans.Csv("files", "walls.csv").Filter("[height] > 3");
        var b = Plans.Csv("files", "walls.csv").Filter("[height] > 4");
        Assert.That(a, Is.Not.EqualTo(b));
        Assert.That(a.Hash, Is.Not.EqualTo(b.Hash));
    }

    [Test]
    public void TextIsCanonical()
    {
        Assert.That(Walls().Text, Is.EqualTo(
            "(select (filter (csv \"files\" \"walls.csv\") \"([height] > 3)\") [\"id\" \"height\"])"));
    }

    [Test]
    public void AliasedTermsRenderWithAnArrow_AndDifferFromPlainOnes()
    {
        var csv = Plans.Csv("files", "walls.csv");
        Assert.That(csv.Select("id", "height as h").Text, Is.EqualTo("(select (csv \"files\" \"walls.csv\") [\"id\" \"height\">\"h\"])"));
        Assert.That(csv.Select("height as h").Hash, Is.Not.EqualTo(csv.Select("height").Hash));
    }

    [TestCase("height", "height", null)]
    [TestCase("  height  ", "height", null)]
    [TestCase("nominal_width as Width_m", "nominal_width", "Width_m")]
    [TestCase("nominal_width AS Width_m", "nominal_width", "Width_m")]
    [TestCase("  Gross Area   As   Area M2 ", "Gross Area", "Area M2")]
    [TestCase("as", "as", null)]
    [TestCase("height as", "height as", null)]
    public void ProjectionParses(string term, string column, string? alias)
        => Assert.That(Projection.Parse(term), Is.EqualTo(new Projection(column, alias)));

    [Test]
    public void ProjectionOutputNameIsTheAliasOrTheColumn()
        => Assert.That((Projection.Parse("a as B").OutputName, Projection.Parse("a").OutputName), Is.EqualTo(("B", "a")));

    [Test]
    public void TextQuotesEmbeddedQuotes()
    {
        var plan = Plans.Table("db", "say \"hi\"");
        Assert.That(plan.Text, Is.EqualTo("(table \"db\" \"say \\\"hi\\\"\")"));
    }

    [Test]
    public void JoinAndAggregateRender()
    {
        var plan = Plans.Table("db", "walls")
            .Join(Plans.Table("db", "levels"), "level_id", "id", JoinKind.Left)
            .Aggregate(["name"], new Aggregation(AggregateFunction.Count, null, "n"), new Aggregation(AggregateFunction.Sum, "height", "total"));
        Assert.That(plan.Text, Is.EqualTo(
            "(aggregate (join Left (table \"db\" \"walls\") (table \"db\" \"levels\") [\"level_id\"=\"id\"]) [\"name\"] [Count(*)>\"n\" Sum(\"height\")>\"total\"])"));
    }

    [Test]
    public void PostOrderListsInputsFirstAndSharedSubtreesOnce()
    {
        var source = Plans.Table("db", "walls");
        var plan = source.Filter("[a] > 1").Join(source.Filter("[a] > 1"), "id", "id");
        var order = plan.PostOrder();
        Assert.That(order.Select(p => p.GetType().Name), Is.EqualTo(new[] { "ReadTable", "Filter", "Join" }));
        Assert.That(plan.Sources(), Is.EqualTo(new[] { source }));
    }

    [Test]
    public void ParseExprRejectsBadText()
        => Assert.Throws<ArgumentException>(() => "[a] +".ParseExpr());
}
