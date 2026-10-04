using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ColorMapNodeTests
{
    [Test]
    public void Numeric_ManualDomain_Matches_ColorScale_Build()
    {
        var values = NodeTestHelpers.Table(
            ("meshVolume", typeof(double), [0.2, 0.5, 1.4, 3.0, null]));

        var table = new ColorMapNode().EvalTable([values],
            ("valueColumn", "meshVolume"), ("colorMap", "viridis"),
            ("auto", "false"), ("min", "0"), ("max", "1"));

        var expected = ColorScale.Build(values.Table, values.Table.ColumnIndex("meshVolume"),
            "viridis", auto: false, min: 0, max: 1, NodeTestHelpers.Ctx, "view.colormap").ToTable();

        Assert.That(table.ColumnCells("role"), Is.EqualTo(expected.ColumnCells("role")));
        Assert.That(table.ColumnCells("label"), Is.EqualTo(expected.ColumnCells("label")));
        Assert.That(table.ColumnCells("value"), Is.EqualTo(expected.ColumnCells("value")));
        Assert.That(table.ColumnCells("r"), Is.EqualTo(expected.ColumnCells("r")));
        Assert.That(table.ColumnCells("g"), Is.EqualTo(expected.ColumnCells("g")));
        Assert.That(table.ColumnCells("b"), Is.EqualTo(expected.ColumnCells("b")));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(expected.ColumnCells("count")));
        Assert.That(table.ColumnCells("column"), Is.EqualTo(expected.ColumnCells("column")));
        Assert.That(table.ColumnCells("domain"), Is.EqualTo(expected.ColumnCells("domain")));
    }

    [Test]
    public void Categorical_Example_Matches_ColorScale_Build()
    {
        var values = NodeTestHelpers.Table(
            ("kind", typeof(string), ["Wall", "Door", "Wall", null]));

        var table = new ColorMapNode().EvalTable([values],
            ("valueColumn", "kind"), ("colorMap", "category10"));

        var expected = ColorScale.Build(values.Table, values.Table.ColumnIndex("kind"),
            "category10", auto: true, min: 0, max: 1, NodeTestHelpers.Ctx, "view.colormap").ToTable();

        Assert.That(table.ColumnCells("role"), Is.EqualTo(expected.ColumnCells("role")));
        Assert.That(table.ColumnCells("label"), Is.EqualTo(expected.ColumnCells("label")));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(expected.ColumnCells("count")));
        Assert.That(table.ColumnCells("domain"), Is.EqualTo(expected.ColumnCells("domain")));
    }

    [Test]
    public void Unknown_Column_Warns_And_Emits_Empty_Legend()
    {
        var values = NodeTestHelpers.Table(("meshVolume", typeof(double), [0.2, 0.5]));

        var (table, warnings) = new ColorMapNode().EvalWithWarnings([values],
            ("valueColumn", "bogus"));

        Assert.That(warnings, Is.EqualTo(new[] { "view.colormap: no column named 'bogus'" }));
        Assert.That(table.RowCount(), Is.EqualTo(0));
        Assert.That(table.ColumnNames(), Is.EqualTo(ColorScale.EmptyTable().ColumnNames()));
    }

    [Test]
    public void Node_Appears_In_Viz_Registry()
        => Assert.That(VizNodes.All.Any(n => n.Spec.Kind == ColorMapNode.Kind), Is.True);
}
