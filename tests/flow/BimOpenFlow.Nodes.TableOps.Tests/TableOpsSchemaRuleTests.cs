using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Tests;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.TableOps.Tests;

/// <summary>The inferred schema of each table operator equals the columns of its evaluated output, so a shape
/// rule and an evaluation cannot drift; plus the failures a rule reports before anything runs.</summary>
[TestFixture]
public sealed class TableOpsSchemaRuleTests
{
    static IDataTable Sample => SampleTables.SampleTable();

    static IDataTable Other()
    {
        var builder = new DataTableBuilder("other");
        builder.AddColumn(new object?[] { "Roof-1" }, "Name", typeof(string));
        builder.AddColumn(new object?[] { 9L }, "Height", typeof(long));
        builder.AddColumn(new object?[] { 5L }, "Count", typeof(long));
        builder.AddColumn(new object?[] { "Roofs" }, "Category", typeof(string));
        return builder.Build();
    }

    [Test]
    public void Filter_KeepsTheShape()
        => SchemaGolden.AssertMatchesEvaluation(new TableFilterNode(), [Sample], ("expr", "[Height] > 2"));

    [Test]
    public void Filter_FailsOnAnUnknownColumnOrANonBooleanPredicate()
    {
        SchemaGolden.AssertFails(new TableFilterNode(), [Sample], "Unknown identifier", ("expr", "[Width] > 2"));
        SchemaGolden.AssertFails(new TableFilterNode(), [Sample], "must be Boolean", ("expr", "[Height] + 1"));
        SchemaGolden.AssertFails(new TableFilterNode(), [Sample], "invalid expression", ("expr", "[Height] >"));
    }

    [TestCase("[Height] * 2", "Number")]
    [TestCase("[Count] * 2", "Integer")]
    [TestCase("[Name] == \"x\"", "Boolean")]
    [TestCase("upper([Name])", "Text")]
    public void Derive_AppendsTheTypedColumn(string expr, string type)
    {
        SchemaGolden.AssertMatchesEvaluation(new TableDeriveNode(), [Sample], ("name", "d"), ("expr", expr));
        var inferred = SchemaGolden.Infer(new TableDeriveNode(), [Sample], ("name", "d"), ("expr", expr));
        Assert.That(inferred.Schema!.Columns[^1].Type.ToString(), Is.EqualTo(type));
    }

    [Test]
    public void Derive_FailsOnAnExistingNameIgnoringCaseOrABadExpression()
    {
        SchemaGolden.AssertFails(new TableDeriveNode(), [Sample], "already exists", ("name", "HEIGHT"), ("expr", "1"));
        SchemaGolden.AssertFails(new TableDeriveNode(), [Sample], "Unknown identifier", ("name", "d"), ("expr", "[Width]"));
    }

    [Test]
    public void Aggregate_MatchesEvaluation()
        => SchemaGolden.AssertMatchesEvaluation(new TableAggregateNode(), [Sample], ("groupBy", "category"),
            ("aggregates", "count(*) as n, sum(Count) as total, avg(Height) as mean, min(Name) as first, max(Height) as tallest, sum(Height) as sumH"));

    [Test]
    public void Aggregate_WithoutGroupsIsOneRowOfAggregates()
        => SchemaGolden.AssertMatchesEvaluation(new TableAggregateNode(), [Sample], ("aggregates", "count(Name) as n"));

    [Test]
    public void Aggregate_FailsOnAMissingColumnOrASumOverText()
    {
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "No column named 'Level'", ("groupBy", "Level"), ("aggregates", "count(*) as n"));
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "No column named 'Width'", ("aggregates", "sum(Width) as w"));
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "Cannot apply Sum to 'Name'", ("aggregates", "sum(Name) as w"));
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "must look like", ("aggregates", "total"));
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "unknown aggregate function", ("aggregates", "median(Height) as m"));
        SchemaGolden.AssertFails(new TableAggregateNode(), [Sample], "duplicate column", ("groupBy", "Category"), ("aggregates", "count(*) as category"));
    }

    [TestCase("Count", "")]
    [TestCase("Name", "descendingA=true")]
    public void Sort_KeepsTheShape(string a, string flag)
        => SchemaGolden.AssertMatchesEvaluation(new TableSortNode(), [Sample], ("A", a), ("B", "Height"),
            ("descendingA", flag.Length > 0 ? "true" : "false"));

    [Test]
    public void Sort_LegacyByTermsAndNoKeys()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableSortNode(), [Sample], ("by", "Count desc, name"));
        SchemaGolden.AssertMatchesEvaluation(new TableSortNode(), [Sample]);
    }

    [Test]
    public void Sort_FailsOnAMissingKey()
        => SchemaGolden.AssertFails(new TableSortNode(), [Sample], "No column named 'Width'", ("A", "Width"));

    [TestCase("boolean")]
    [TestCase("integer")]
    [TestCase("number")]
    [TestCase("text")]
    public void Cast_InPlaceMatchesEvaluation(string type)
        => SchemaGolden.AssertMatchesEvaluation(new TableCastNode(), [Sample], ("column", "count"), ("type", type), ("onError", "null"));

    [TestCase("date")]
    [TestCase("datetime")]
    public void Cast_DatesComeBackAsText(string type)
    {
        var dates = new DataTableBuilder("d");
        dates.AddColumn(new object?[] { "2026-01-02", null }, "When", typeof(string));
        SchemaGolden.AssertMatchesEvaluation(new TableCastNode(), [dates.Build()], ("column", "When"), ("type", type));
    }

    [Test]
    public void Cast_AsANewColumn()
        => SchemaGolden.AssertMatchesEvaluation(new TableCastNode(), [Sample], ("column", "Count"), ("type", "text"), ("name", "CountText"));

    [Test]
    public void Cast_FailsOnAMissingColumnOrAnExistingName()
    {
        SchemaGolden.AssertFails(new TableCastNode(), [Sample], "No column named 'Width'", ("column", "Width"), ("type", "text"));
        SchemaGolden.AssertFails(new TableCastNode(), [Sample], "already exists", ("column", "Count"), ("type", "text"), ("name", "name"));
        SchemaGolden.AssertFails(new TableCastNode(), [Sample], "'type'", ("column", "Count"));
    }

    [Test]
    public void Concat_StrictMatchesEvaluation()
        => SchemaGolden.AssertMatchesEvaluation(new TableConcatNode(), [Sample, Other()]);

    [Test]
    public void Concat_ByNameMatchesEvaluation()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "x" }, "category", typeof(string));
        b.AddColumn(new object?[] { true }, "Flag", typeof(bool));
        SchemaGolden.AssertMatchesEvaluation(new TableConcatNode(), [Sample, b.Build()], ("columns", "byName"));
    }

    [Test]
    public void Concat_StrictFailsOnDifferentColumns()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { "x" }, "Name", typeof(string));
        SchemaGolden.AssertFails(new TableConcatNode(), [Sample, b.Build()], "identical column sequences");
    }

    [Test]
    public void Concat_ByNameFailsOnIncompatibleTypes()
    {
        var b = new DataTableBuilder("b");
        b.AddColumn(new object?[] { 1L }, "Name", typeof(long));
        SchemaGolden.AssertFails(new TableConcatNode(), [Sample, b.Build()], "Column 'Name' is Text", ("columns", "byName"));
    }

    [Test]
    public void Distinct_MatchesEvaluation()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableDistinctNode(), [Sample]);
        SchemaGolden.AssertMatchesEvaluation(new TableDistinctNode(), [Sample], ("columns", "category"));
        SchemaGolden.AssertFails(new TableDistinctNode(), [Sample], "No column named 'Width'", ("columns", "Width"));
    }

    [Test]
    public void Drop_MatchesEvaluation()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableDropNode(), [Sample], ("columns", "height, CATEGORY"));
        Assert.That(SchemaGolden.Warnings(new TableDropNode(), [Sample], ("columns", "Width")), Has.Some.Contain("No column named 'Width'"));
        SchemaGolden.AssertFails(new TableDropNode(), [Sample], "empty table", ("columns", "Name, Height, Count, Category"));
    }

    [Test]
    public void Limit_KeepsTheShapeAndChecksItsParameters()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableLimitNode(), [Sample], ("count", "2"), ("offset", "1"));
        SchemaGolden.AssertFails(new TableLimitNode(), [Sample], "'count' must be a non-negative integer", ("count", "-1"));
    }

    [Test]
    public void Rename_MatchesEvaluationKeepingTheStoredSpellingOfTheRest()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableRenameNode(), [Sample], ("renames", "name=Label, HEIGHT=Metres"));
        Assert.That(SchemaGolden.Warnings(new TableRenameNode(), [Sample], ("renames", "Width=W")), Has.Some.Contain("No column named 'Width'"));
        SchemaGolden.AssertFails(new TableRenameNode(), [Sample], "duplicate column", ("renames", "Name=Count"));
        SchemaGolden.AssertFails(new TableRenameNode(), [Sample], "renamed more than once", ("renames", "Name=A, name=B"));
        SchemaGolden.AssertFails(new TableRenameNode(), [Sample], "cannot parse rename", ("renames", "Name"));
    }

    [Test]
    public void ChainedInferenceNeedsNoData()
    {
        var schema = TableSchemas.Of(Sample);
        var filtered = SchemaGolden.Infer(new TableFilterNode(), [schema], ("expr", "[Count] > 1"));
        var grouped = SchemaGolden.Infer(new TableAggregateNode(), [filtered.Schema!], ("groupBy", "Category"), ("aggregates", "sum(Count) as total"));
        Assert.That(grouped.ToString(), Is.EqualTo("Category:Text?, total:Integer?"));
    }
}
