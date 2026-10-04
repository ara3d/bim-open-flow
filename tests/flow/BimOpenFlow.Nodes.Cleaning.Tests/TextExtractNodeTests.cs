using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TextExtractNodeTests
{
    private static TableValue Labels()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "Level 03 - Zone B", "Roof", "Level 12", null }, "label", typeof(string));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Extracts_Group_One_And_Nulls_Non_Matches()
    {
        var table = new TextExtractNode().EvalTable([Labels()],
            ("column", "label"), ("pattern", @"Level (\d+)"), ("name", "level"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "label", "level" }));
        Assert.That(table.Cell("level", 0), Is.EqualTo("03"));
        Assert.That(table.Cell("level", 1), Is.Null, "no match yields null");
        Assert.That(table.Cell("level", 2), Is.EqualTo("12"));
        Assert.That(table.Cell("level", 3), Is.Null, "null input yields null");
    }

    [Test]
    public void Group_Zero_Is_The_Whole_Match()
    {
        var table = new TextExtractNode().EvalTable([Labels()],
            ("column", "label"), ("pattern", @"Level (\d+)"), ("group", "0"), ("name", "match"));
        Assert.That(table.Cell("match", 0), Is.EqualTo("Level 03"));
    }

    [Test]
    public void Input_Columns_And_Order_Are_Preserved()
    {
        var table = new TextExtractNode().EvalTable([Labels()],
            ("column", "label"), ("pattern", @"Zone (\w)"), ("name", "zone"));
        Assert.That(table.Rows.Count, Is.EqualTo(4));
        Assert.That(table.Cell("label", 1), Is.EqualTo("Roof"));
        Assert.That(table.Cell("zone", 0), Is.EqualTo("B"));
    }

    [Test]
    public void Invalid_Regex_Is_An_Error()
    {
        Assert.That(
            () => new TextExtractNode().EvalTable([Labels()],
                ("column", "label"), ("pattern", "("), ("name", "x")),
            Throws.ArgumentException.With.Message.StartsWith("text.extract:"));
    }

    [Test]
    public void Existing_Name_Is_An_Error()
    {
        Assert.That(
            () => new TextExtractNode().EvalTable([Labels()],
                ("column", "label"), ("pattern", @"(\d+)"), ("name", "label")),
            Throws.ArgumentException.With.Message.StartsWith("text.extract:"));
    }

    [Test]
    public void Missing_Name_Is_An_Error()
    {
        Assert.That(
            () => new TextExtractNode().EvalTable([Labels()],
                ("column", "label"), ("pattern", @"(\d+)")),
            Throws.ArgumentException.With.Message.StartsWith("text.extract:"));
    }

    [Test]
    public void Negative_Group_Is_An_Error()
    {
        Assert.That(
            () => new TextExtractNode().EvalTable([Labels()],
                ("column", "label"), ("pattern", @"(\d+)"), ("group", "-1"), ("name", "x")),
            Throws.ArgumentException.With.Message.StartsWith("text.extract:"));
    }
}
