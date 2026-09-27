using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ChartBarNodeTests
{
    [Test]
    public void Projects_Label_Then_Values_In_Param_Order()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "cost, count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "cost", "count" }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "b", "a", "c" }));
    }

    [Test]
    public void Trims_Comma_Separated_Value_Columns()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", " cost ,  count "));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "cost", "count" }));
    }

    [Test]
    public void Sorts_Ascending_By_First_Value_Column_Numerically()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "count"), ("sort", "asc"));
        Assert.That(table.ColumnCells("count"), Is.EqualTo(new[] { 1L, 2L, 3L }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    [Test]
    public void Sorts_Descending_By_First_Value_Column()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "cost, count"), ("sort", "desc"));
        Assert.That(table.ColumnCells("cost"), Is.EqualTo(new[] { 2.5, 1.5, 0.5 }));
        Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "c", "b", "a" }));
    }

    [Test]
    public void Sorts_Text_Value_Column_Ordinally()
    {
        var input = NodeTestHelpers.Table(
            ("label", typeof(string), ["x", "y", "z"]),
            ("grade", typeof(string), ["b", "a", "c"]));
        var table = new ChartBarNode().EvalTable([input],
            ("labelColumn", "label"), ("valueColumns", "grade"), ("sort", "asc"));
        Assert.That(table.ColumnCells("grade"), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(table.ColumnCells("label"), Is.EqualTo(new[] { "y", "x", "z" }));
    }

    [Test]
    public void Unknown_Value_Column_Warns_And_Is_Skipped()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("labelColumn", "name"), ("valueColumns", "count, bogus"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.bar: no column named 'bogus'" }));
    }

    [Test]
    public void Empty_Label_Falls_Back_To_First_Text_Column_Without_Warning()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("valueColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.Empty);
    }

    [Test]
    public void Absent_Label_Warns_And_Falls_Back_To_First_Text_Column()
    {
        var (table, warnings) = new ChartBarNode().EvalWithWarnings([VizTestTables.Sample()],
            ("labelColumn", "bogus"), ("valueColumns", "count"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count" }));
        Assert.That(warnings, Is.EqualTo(new[] { "chart.bar: no column named 'bogus'" }));
    }

    [Test]
    public void Empty_Value_Columns_Default_To_All_Numeric_Except_Label()
    {
        var table = new ChartBarNode().EvalTable([VizTestTables.Sample()],
            ("labelColumn", "name"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "count", "cost" }));
    }

    [Test]
    public void Sorting_Does_Not_Mutate_The_Input_Table()
    {
        var input = VizTestTables.Sample();
        new ChartBarNode().EvalTable([input],
            ("labelColumn", "name"), ("valueColumns", "count"), ("sort", "asc"));
        Assert.That(input.Table.ColumnCells("count"), Is.EqualTo(new[] { 2L, 3L, 1L }));
        Assert.That(input.Table.ColumnNames(), Is.EqualTo(new[] { "name", "count", "cost" }));
    }

    /// <summary>The worked example from the shared-colour-legend plan: viridis,
    /// a manual 0..1 domain, over [0.2, 0.5, 1.4, 3.0, null].</summary>
    private static (TableValue Bars, ColorScale Scale) WorkedExample()
    {
        var bars = NodeTestHelpers.Table(
            ("name", typeof(string), ["a", "b", "c", "d", "e"]),
            ("meshVolume", typeof(double), [0.2, 0.5, 1.4, 3.0, null]));
        var scale = ColorScale.Build(bars.Table, bars.Table.ColumnIndex("meshVolume"),
            "viridis", auto: false, min: 0, max: 1, NodeTestHelpers.Ctx, "chart.bar");
        return (bars, scale);
    }

    [Test]
    public void Scale_Input_Colours_Bars_And_Passes_The_Legend_Through()
    {
        var (bars, scale) = WorkedExample();
        var scaleTable = new TableValue(scale.ToTable());

        var outputs = new ChartBarNode().Eval(NodeTestHelpers.Ctx,
            [bars, scaleTable],
            NodeTestHelpers.Params(("labelColumn", "name"), ("valueColumns", "meshVolume")));
        var table = ((TableValue)outputs[0]).Table;
        var legend = ((TableValue)outputs[1]).Table;

        var expectedColors = bars.Table.ColumnCells("meshVolume").Select(scale.ColorOf).ToList();
        Assert.That(table.ColumnCells("r"), Is.EqualTo(expectedColors.Select(c => c.R).ToArray()));
        Assert.That(table.ColumnCells("g"), Is.EqualTo(expectedColors.Select(c => c.G).ToArray()));
        Assert.That(table.ColumnCells("b"), Is.EqualTo(expectedColors.Select(c => c.B).ToArray()));

        var expectedLegend = scale.ToTable();
        Assert.That(legend.ColumnNames(), Is.EqualTo(expectedLegend.ColumnNames()));
        foreach (var column in legend.ColumnNames())
            Assert.That(legend.ColumnCells(column), Is.EqualTo(expectedLegend.ColumnCells(column)));
    }

    [Test]
    public void No_Scale_Input_Emits_The_Empty_Legend_Table()
    {
        var outputs = new ChartBarNode().Eval(NodeTestHelpers.Ctx,
            [VizTestTables.Sample()],
            NodeTestHelpers.Params(("labelColumn", "name"), ("valueColumns", "cost")));
        var legend = ((TableValue)outputs[1]).Table;

        Assert.That(legend.ColumnNames(), Is.EqualTo(ColorScale.EmptyTable().ColumnNames()));
        Assert.That(legend.RowCount(), Is.EqualTo(0));
    }

    [Test]
    public void PreExisting_R_Column_Is_Replaced_With_A_Warning()
    {
        var (values, scale) = WorkedExample();
        var bars = NodeTestHelpers.Table(
            ("name", typeof(string), ["a", "b", "c", "d", "e"]),
            ("meshVolume", typeof(double), [0.2, 0.5, 1.4, 3.0, null]),
            ("r", typeof(double), [9.0, 9.0, 9.0, 9.0, 9.0]));
        var scaleTable = new TableValue(scale.ToTable());
        var ctx = new FakeEvalContext();

        var outputs = new ChartBarNode().Eval(ctx, [bars, scaleTable],
            NodeTestHelpers.Params(("labelColumn", "name"), ("valueColumns", "meshVolume, r")));
        var table = ((TableValue)outputs[0]).Table;

        Assert.That(table.ColumnCells("r"), Is.Not.EqualTo(new object?[] { 9.0, 9.0, 9.0, 9.0, 9.0 }));
        Assert.That(ctx.Warnings.Any(w => w.Contains("column(s) r")), Is.True);
        _ = values;
    }

    [Test]
    public void Bars_Outside_The_Domain_Warn()
    {
        var (bars, scale) = WorkedExample();
        var scaleTable = new TableValue(scale.ToTable());
        var ctx = new FakeEvalContext();

        new ChartBarNode().Eval(ctx, [bars, scaleTable],
            NodeTestHelpers.Params(("labelColumn", "name"), ("valueColumns", "meshVolume")));

        Assert.That(ctx.Warnings.Any(w => w.Contains("lie outside")), Is.True);
    }

    [Test]
    public void Missing_Scale_Column_Falls_Back_To_The_First_Value_Column_With_A_Warning()
    {
        var scale = ColorScale.Build(
            NodeTestHelpers.Table(("meshVolume", typeof(double), [0.2, 0.5])).Table, 0,
            "viridis", auto: false, min: 0, max: 1, NodeTestHelpers.Ctx, "chart.bar");
        var bars = NodeTestHelpers.Table(
            ("name", typeof(string), ["a", "b"]),
            ("cost", typeof(double), [0.2, 0.5]));
        var scaleTable = new TableValue(scale.ToTable());
        var ctx = new FakeEvalContext();

        var outputs = new ChartBarNode().Eval(ctx, [bars, scaleTable],
            NodeTestHelpers.Params(("labelColumn", "name"), ("valueColumns", "cost")));
        var table = ((TableValue)outputs[0]).Table;

        Assert.That(table.ColumnCells("r"), Is.EqualTo(new[] { 0.2, 0.5 }.Select(v => scale.ColorOf(v).R)));
        Assert.That(ctx.Warnings.Any(w => w.Contains("no column named 'meshVolume'")), Is.True);
    }
}
