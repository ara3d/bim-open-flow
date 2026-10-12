using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Tests;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.TableOps.Tests;

/// <summary>Window, split, unpivot, sample, profile and schema: the inferred (or declared) schema equals the
/// columns of the evaluated output; the nodes whose run skips a missing column warn at inference too.</summary>
[TestFixture]
public sealed class TableOpsReshapeSchemaTests
{
    static IDataTable Sample => SampleTables.SampleTable();

    static IDataTable Wide()
    {
        var builder = new DataTableBuilder("wide");
        builder.AddColumn(new object?[] { "a", "b" }, "id", typeof(string));
        builder.AddColumn(new object?[] { 1L, 2L }, "x", typeof(long));
        builder.AddColumn(new object?[] { 3L, null }, "y", typeof(long));
        builder.AddColumn(new object?[] { 0.5, 1.5 }, "z", typeof(double));
        builder.AddColumn(new object?[] { "p", "q" }, "note", typeof(string));
        return builder.Build();
    }

    static IDataTable Parts()
    {
        var builder = new DataTableBuilder("parts");
        builder.AddColumn(new object?[] { "W-01-A", "D-02", null }, "code", typeof(string));
        builder.AddColumn(new object?[] { 1L, 2L, 3L }, "n", typeof(long));
        return builder.Build();
    }

    [TestCase("rowNumber", "")]
    [TestCase("rank", "")]
    [TestCase("denseRank", "")]
    [TestCase("lag", "Height")]
    [TestCase("lead", "Name")]
    [TestCase("cumSum", "Count")]
    [TestCase("cumSum", "Height")]
    [TestCase("movingAvg", "Count")]
    [TestCase("percentOfTotal", "Count")]
    public void Window_MatchesEvaluation(string function, string column)
        => SchemaGolden.AssertMatchesEvaluation(new TableWindowNode(), [Sample], ("function", function), ("column", column),
            ("name", "w"), ("partitionBy", "Category"), ("orderBy", "Count desc"));

    [Test]
    public void Window_FailsOnAnExistingNameAMissingColumnOrANonNumericSum()
    {
        SchemaGolden.AssertFails(new TableWindowNode(), [Sample], "already exists", ("function", "rank"), ("name", "COUNT"));
        SchemaGolden.AssertFails(new TableWindowNode(), [Sample], "No column named 'Width'", ("function", "lag"), ("column", "Width"), ("name", "w"));
        SchemaGolden.AssertFails(new TableWindowNode(), [Sample], "did you mean 'Category'", ("function", "rank"), ("name", "w"), ("partitionBy", "Categry"));
        SchemaGolden.AssertFails(new TableWindowNode(), [Sample], "Cannot apply CumSum to 'Name'", ("function", "cumSum"), ("column", "Name"), ("name", "w"));
        SchemaGolden.AssertFails(new TableWindowNode(), [Sample], "'windowSize' must be at least 1", ("function", "movingAvg"), ("column", "Count"), ("name", "w"), ("windowSize", "0"));
    }

    [Test]
    public void SplitColumn_MatchesEvaluationWithAndWithoutTheSource()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableSplitColumnNode(), [Parts()], ("column", "code"), ("names", "kind, level, suffix"));
        SchemaGolden.AssertMatchesEvaluation(new TableSplitColumnNode(), [Parts()], ("column", "CODE"), ("names", "kind, level"), ("keep", "true"));
    }

    [Test]
    public void SplitColumn_Fails()
    {
        SchemaGolden.AssertFails(new TableSplitColumnNode(), [Parts()], "No column named 'cod'", ("column", "cod"), ("names", "a"));
        SchemaGolden.AssertFails(new TableSplitColumnNode(), [Parts()], "already exists", ("column", "code"), ("names", "N"));
        SchemaGolden.AssertFails(new TableSplitColumnNode(), [Parts()], "named more than once", ("column", "code"), ("names", "a, A"));
        SchemaGolden.AssertFails(new TableSplitColumnNode(), [Parts()], "is Integer, not Text", ("column", "n"), ("names", "a"));
        SchemaGolden.AssertFails(new TableSplitColumnNode(), [Parts()], "must not be empty", ("column", "code"), ("names", "a"), ("separator", ""));
    }

    [Test]
    public void Unpivot_SameKindKeepsTheKind()
        => SchemaGolden.AssertMatchesEvaluation(new TableUnpivotNode(), [Wide()], ("keep", "id"), ("columns", "x, y"));

    [Test]
    public void Unpivot_MixedKindsWidenToTextWithAWarning()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableUnpivotNode(), [Wide()], ("keep", "id"), ("columns", "x, z"),
            ("nameColumn", "field"), ("valueColumn", "v"));
        Assert.That(SchemaGolden.Warnings(new TableUnpivotNode(), [Wide()], ("keep", "id"), ("columns", "x, z")),
            Has.Some.Contain("mixed types"));
    }

    [Test]
    public void Unpivot_WithoutColumnsFoldsEverythingNotKept()
        => SchemaGolden.AssertMatchesEvaluation(new TableUnpivotNode(), [Wide()], ("keep", "id, note"), ("columns", ""));

    [Test]
    public void Unpivot_WarnsAboutDroppedColumns()
        => Assert.That(SchemaGolden.Warnings(new TableUnpivotNode(), [Wide()], ("keep", "id"), ("columns", "x, y")),
            Has.Some.Contain("dropped: z, note"));

    [Test]
    public void Unpivot_Fails()
    {
        SchemaGolden.AssertFails(new TableUnpivotNode(), [Wide()], "No column named 'w'", ("keep", "id"), ("columns", "w"));
        SchemaGolden.AssertFails(new TableUnpivotNode(), [Wide()], "collides with a kept column", ("keep", "id"), ("columns", "x"), ("nameColumn", "ID"));
        SchemaGolden.AssertFails(new TableUnpivotNode(), [Wide()], "No columns to unpivot", ("keep", "id, x, y, z, note"));
    }

    [Test]
    public void Sample_KeepsTheShape()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableSampleNode(), [Sample], ("rows", "2"));
        SchemaGolden.AssertMatchesEvaluation(new TableSampleNode(), [Sample], ("mode", "fraction"), ("fraction", "0.5"));
        SchemaGolden.AssertFails(new TableSampleNode(), [Sample], "between 0 and 1", ("mode", "fraction"), ("fraction", "2"));
    }

    [Test]
    public void Profile_DeclaresItsColumnsWhateverTheInput()
    {
        SchemaGolden.AssertDeclaredMatchesEvaluation(new TableProfileNode(), [Sample]);
        SchemaGolden.AssertDeclaredMatchesEvaluation(new TableProfileNode(), [Wide()]);
    }

    [Test]
    public void SchemaNode_DeclaresItsColumnsWhateverTheInput()
    {
        SchemaGolden.AssertDeclaredMatchesEvaluation(new TableSchemaNode(), [Sample]);
        SchemaGolden.AssertDeclaredMatchesEvaluation(new TableSchemaNode(), [Wide()]);
    }

    static IDataTable WithDates()
    {
        var builder = new DataTableBuilder("dates");
        builder.AddColumn(new object?[] { new DateOnly(2026, 1, 2), null }, "built", typeof(DateOnly));
        builder.AddColumn(new object?[] { "a", "b" }, "id", typeof(string));
        return builder.Build();
    }

    [Test]
    public void DuckDbBackedNodesHandDatesBackAsText()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableSampleNode(), [WithDates()], ("rows", "5"));
        SchemaGolden.AssertMatchesEvaluation(new TableLimitNode(), [WithDates()], ("count", "5"));
        SchemaGolden.AssertMatchesEvaluation(new TableSortNode(), [WithDates()], ("A", "id"));
        SchemaGolden.AssertMatchesEvaluation(new TableDistinctNode(), [WithDates()]);
        SchemaGolden.AssertMatchesEvaluation(new TableRenameNode(), [WithDates()], ("renames", "id=key"));
        SchemaGolden.AssertMatchesEvaluation(new TableDropNode(), [WithDates()], ("columns", "id"));
        SchemaGolden.AssertMatchesEvaluation(new TableWindowNode(), [WithDates()], ("function", "lag"), ("column", "built"), ("name", "prev"));
        SchemaGolden.AssertMatchesEvaluation(new TableAggregateNode(), [WithDates()], ("aggregates", "max(built) as latest"));
    }

    [Test]
    public void Drop_AndRenameWarnNamingTheMissingColumnAndTheNearestOne()
    {
        Assert.That(SchemaGolden.Warnings(new TableDropNode(), [Sample], ("columns", "Heigth, Category")),
            Is.EqualTo(new[] { "No column named 'Heigth'; did you mean 'Height'?" }));
        Assert.That(SchemaGolden.Warnings(new TableRenameNode(), [Sample], ("renames", "Nme=Label")),
            Is.EqualTo(new[] { "No column named 'Nme'; did you mean 'Name'?" }));
        SchemaGolden.AssertMatchesEvaluation(new TableDropNode(), [Sample], ("columns", "Heigth, Category"));
        SchemaGolden.AssertMatchesEvaluation(new TableRenameNode(), [Sample], ("renames", "Nme=Label, Count=N"));
    }

    [Test]
    public void Derive_AnAlwaysNullExpressionFailsAsTheRunDoes()
    {
        SchemaGolden.AssertFails(new TableDeriveNode(), [Sample], "always null", ("name", "d"), ("expr", "null"));
        Assert.That(() => new TableDeriveNode().EvalTable(Sample, ("name", "d"), ("expr", "null")),
            Throws.ArgumentException.With.Message.Contain("always null"));
    }
}
