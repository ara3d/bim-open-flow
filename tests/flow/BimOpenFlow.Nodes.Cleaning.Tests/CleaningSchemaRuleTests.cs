using Ara3D.DataTable;
using BimOpenFlow.Nodes.Tests;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

/// <summary>The inferred schema of each cleaning node equals the columns of its evaluated output.</summary>
[TestFixture]
public sealed class CleaningSchemaRuleTests
{
    static IDataTable Rows()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "d1", "d2", "d1", null }, "id", typeof(string));
        builder.AddColumn(new object?[] { 1L, null, 3L, 4L }, "rev", typeof(long));
        builder.AddColumn(new object?[] { 1.5, 2.5, null, 4.5 }, "h", typeof(double));
        builder.AddColumn(new object?[] { true, null, false, true }, "ok", typeof(bool));
        builder.AddColumn(new object?[] { " A-1 ", "b-22", null, "c" }, "label", typeof(string));
        return builder.Build();
    }

    [Test]
    public void Dedupe_KeepsTheShape()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableDedupeNode(), [Rows()], ("keys", "id"), ("orderBy", "rev desc"));
        SchemaGolden.AssertFails(new TableDedupeNode(), [Rows()], "did you mean 'rev'", ("keys", "id"), ("orderBy", "rv"));
        SchemaGolden.AssertFails(new TableDedupeNode(), [Rows()], "No column named 'zz'", ("keys", "zz"));
    }

    [TestCase("any", "rev")]
    [TestCase("any", "")]
    [TestCase("all", "rev, h")]
    [TestCase("all", "rev")]
    public void DropNulls_MatchesEvaluationIncludingNullability(string mode, string columns)
        => SchemaGolden.AssertMatchesEvaluation(new TableDropNullsNode(), [Rows()], ("mode", mode), ("columns", columns));

    [Test]
    public void DropNulls_FailsOnAMissingColumn()
        => SchemaGolden.AssertFails(new TableDropNullsNode(), [Rows()], "No column named 'x'", ("columns", "x"));

    [TestCase("rev", "7")]
    [TestCase("h", "0.5")]
    [TestCase("ok", "true")]
    [TestCase("id, label", "none")]
    public void FillNulls_ConstantMatchesEvaluationAndIsNeverNull(string columns, string value)
        => SchemaGolden.AssertMatchesEvaluation(new TableFillNullsNode(), [Rows()], ("columns", columns), ("value", value));

    [Test]
    public void FillNulls_DirectionalKeepsTheShape()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableFillNullsNode(), [Rows()], ("columns", "rev, h"), ("strategy", "forward"));
        SchemaGolden.AssertMatchesEvaluation(new TableFillNullsNode(), [Rows()], ("columns", "rev"), ("strategy", "backward"), ("partitionBy", "id"));
    }

    [Test]
    public void FillNulls_Fails()
    {
        SchemaGolden.AssertFails(new TableFillNullsNode(), [Rows()], "cannot be cast", ("columns", "rev"), ("value", "x"));
        SchemaGolden.AssertFails(new TableFillNullsNode(), [Rows()], "No column named 'nope'", ("columns", "nope"), ("value", "1"));
    }

    [TestCase("exact")]
    [TestCase("substring")]
    [TestCase("regex")]
    public void Replace_KeepsTheShape(string match)
        => SchemaGolden.AssertMatchesEvaluation(new TableReplaceNode(), [Rows()], ("column", "label"), ("find", "b"), ("replaceWith", "B"), ("match", match));

    [Test]
    public void Replace_NeedsATextColumn()
    {
        SchemaGolden.AssertFails(new TableReplaceNode(), [Rows()], "not a text column", ("column", "rev"), ("find", "1"));
        SchemaGolden.AssertFails(new TableReplaceNode(), [Rows()], "No column named 'lable'; did you mean 'label'", ("column", "lable"), ("find", "1"));
    }

    [Test]
    public void Extract_AppendsANullableTextColumn()
    {
        SchemaGolden.AssertMatchesEvaluation(new TextExtractNode(), [Rows()], ("column", "label"), ("pattern", @"(\d+)"), ("name", "num"));
        SchemaGolden.AssertFails(new TextExtractNode(), [Rows()], "already exists", ("column", "label"), ("pattern", "x"), ("name", "REV"));
        SchemaGolden.AssertFails(new TextExtractNode(), [Rows()], "not a text column", ("column", "rev"), ("pattern", "x"), ("name", "n"));
        SchemaGolden.AssertFails(new TextExtractNode(), [Rows()], "'group' must be a non-negative", ("column", "label"), ("pattern", "x"), ("name", "n"), ("group", "-1"));
    }

    [Test]
    public void Transform_KeepsTheShape()
    {
        SchemaGolden.AssertMatchesEvaluation(new TextTransformNode(), [Rows()], ("op", "upper"));
        SchemaGolden.AssertMatchesEvaluation(new TextTransformNode(), [Rows()], ("columns", "label, id"), ("op", "trim"));
        SchemaGolden.AssertFails(new TextTransformNode(), [Rows()], "not a text column", ("columns", "rev"));
    }

    static IDataTable WithDates()
    {
        var builder = new DataTableBuilder("dates");
        builder.AddColumn(new object?[] { new DateOnly(2026, 1, 2), null }, "built", typeof(DateOnly));
        builder.AddColumn(new object?[] { "a", "b" }, "id", typeof(string));
        return builder.Build();
    }

    [Test]
    public void DatesComeBackAsText()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableDedupeNode(), [WithDates()], ("keys", "id"));
        SchemaGolden.AssertMatchesEvaluation(new TableDropNullsNode(), [WithDates()]);
        SchemaGolden.AssertMatchesEvaluation(new TextTransformNode(), [WithDates()]);
        SchemaGolden.AssertMatchesEvaluation(new TableFillNullsNode(), [WithDates()], ("columns", "id"), ("value", "z"));
    }
}
