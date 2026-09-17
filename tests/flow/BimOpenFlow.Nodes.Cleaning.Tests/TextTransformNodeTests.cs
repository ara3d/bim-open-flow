using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TextTransformNodeTests
{
    private static TableValue Messy()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "  Level 01 ", "a\t b", null }, "name", typeof(string));
        builder.AddColumn(new object?[] { " x ", "Y", "z" }, "code", typeof(string));
        builder.AddColumn(new object?[] { 1L, 2L, 3L }, "n", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Trim_Applies_To_Named_Column_Only()
    {
        var table = new TextTransformNode().EvalTable([Messy()],
            ("columns", "name"), ("op", "trim"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("Level 01"));
        Assert.That(table.Cell("code", 0), Is.EqualTo(" x "), "unlisted column untouched");
        Assert.That(table.Cell("name", 2), Is.Null);
    }

    [Test]
    public void Empty_Columns_Transforms_Every_Text_Column()
    {
        var table = new TextTransformNode().EvalTable([Messy()], ("op", "upper"));
        Assert.That(table.Cell("name", 1), Is.EqualTo("A\t B"));
        Assert.That(table.Cell("code", 0), Is.EqualTo(" X "));
        Assert.That(table.Cell("n", 0), Is.EqualTo(1L), "non-text column untouched");
    }

    [Test]
    public void Lower_Lowers()
    {
        var table = new TextTransformNode().EvalTable([Messy()],
            ("columns", "code"), ("op", "lower"));
        Assert.That(table.Cell("code", 1), Is.EqualTo("y"));
    }

    [Test]
    public void NormalizeSpace_Trims_And_Collapses_Whitespace()
    {
        var table = new TextTransformNode().EvalTable([Messy()],
            ("columns", "name"), ("op", "normalizeSpace"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("Level 01"));
        Assert.That(table.Cell("name", 1), Is.EqualTo("a b"));
    }

    [Test]
    public void Named_Non_Text_Column_Is_An_Error()
    {
        Assert.That(
            () => new TextTransformNode().EvalTable([Messy()], ("columns", "n"), ("op", "trim")),
            Throws.ArgumentException.With.Message.StartsWith("text.transform:"));
    }

    [Test]
    public void Bad_Op_Is_An_Error()
    {
        Assert.That(
            () => new TextTransformNode().EvalTable([Messy()], ("op", "shout")),
            Throws.ArgumentException.With.Message.StartsWith("text.transform:"));
    }
}
