using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Cleaning.Tests;

[TestFixture]
public class TableReplaceNodeTests
{
    private static TableValue Names()
    {
        var builder = new DataTableBuilder("t");
        builder.AddColumn(new object?[] { "N/A", "Level 01", "n/a", null, "Aan/Aant" }, "name", typeof(string));
        builder.AddColumn(new object?[] { 1L, 2L, 3L, 4L, 5L }, "n", typeof(long));
        return new TableValue(builder.Build());
    }

    [Test]
    public void Exact_Match_Recode_Only_Replaces_Whole_Values()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", "N/A"), ("replaceWith", ""));
        Assert.That(table.Cell("name", 0), Is.EqualTo(""));
        Assert.That(table.Cell("name", 2), Is.EqualTo("n/a"), "case-sensitive by default");
        Assert.That(table.Cell("name", 4), Is.EqualTo("Aan/Aant"), "no substring effect");
        Assert.That(table.Cell("name", 3), Is.Null, "nulls pass through");
    }

    [Test]
    public void Exact_Match_Case_Insensitive_Recodes_Both_Casings()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", "n/a"), ("replaceWith", "missing"), ("caseSensitive", "false"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("missing"));
        Assert.That(table.Cell("name", 2), Is.EqualTo("missing"));
        Assert.That(table.Cell("name", 4), Is.EqualTo("Aan/Aant"));
    }

    [Test]
    public void Substring_Replaces_All_Occurrences()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", "Aa"), ("replaceWith", "B"), ("match", "substring"));
        Assert.That(table.Cell("name", 4), Is.EqualTo("Bn/Bnt"));
        Assert.That(table.Cell("name", 1), Is.EqualTo("Level 01"));
    }

    [Test]
    public void Substring_Case_Insensitive_Treats_Find_Literally()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", "n/a"), ("replaceWith", "-"),
            ("match", "substring"), ("caseSensitive", "false"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("-"));
        Assert.That(table.Cell("name", 2), Is.EqualTo("-"));
        Assert.That(table.Cell("name", 4), Is.EqualTo("Aa-ant"));
    }

    [Test]
    public void Regex_Replaces_With_Group_References()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", @"Level (\d+)"), ("replaceWith", @"L\1"), ("match", "regex"));
        Assert.That(table.Cell("name", 1), Is.EqualTo("L01"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("N/A"));
    }

    [Test]
    public void Regex_Case_Insensitive_Matches_Both_Casings()
    {
        var table = new TableReplaceNode().EvalTable([Names()],
            ("column", "name"), ("find", "^n/a$"), ("replaceWith", "x"),
            ("match", "regex"), ("caseSensitive", "false"));
        Assert.That(table.Cell("name", 0), Is.EqualTo("x"));
        Assert.That(table.Cell("name", 2), Is.EqualTo("x"));
    }

    [Test]
    public void Non_Text_Column_Is_An_Error()
    {
        Assert.That(
            () => new TableReplaceNode().EvalTable([Names()],
                ("column", "n"), ("find", "1"), ("replaceWith", "2")),
            Throws.ArgumentException.With.Message.StartsWith("table.replace:"));
    }

    [Test]
    public void Invalid_Regex_Is_An_Error()
    {
        Assert.That(
            () => new TableReplaceNode().EvalTable([Names()],
                ("column", "name"), ("find", "("), ("replaceWith", ""), ("match", "regex")),
            Throws.ArgumentException.With.Message.StartsWith("table.replace:"));
    }
}
