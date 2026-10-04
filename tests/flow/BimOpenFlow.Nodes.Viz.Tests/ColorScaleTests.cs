using BimOpenFlow.Nodes.Support;

namespace BimOpenFlow.Nodes.Viz.Tests;

[TestFixture]
public class ColorScaleTests
{
    [Test]
    public void Gradient_Manual_Domain_Produces_Worked_Example_Rows_And_One_Warning()
    {
        var table = NodeTestHelpers.Table(
            ("meshVolume", typeof(double), [0.2, 0.5, 1.4, 3.0, null])).Table;
        var ctx = new FakeEvalContext();

        var scale = ColorScale.Build(table, table.ColumnIndex("meshVolume"), "viridis",
            auto: false, min: 0, max: 1, ctx, "test.kind");

        Assert.That(scale.Domain, Is.EqualTo("manual"));
        var stops = scale.Rows.Where(r => r.Role == "stop").ToList();
        Assert.That(stops.Select(r => r.Value), Is.EqualTo(new double?[] { 0, 0.25, 0.5, 0.75, 1 }));
        Assert.That(stops.Select(r => r.Color), Is.EqualTo(ColorMaps.ViridisStops));

        var above = scale.Rows.Single(r => r.Role == "above");
        Assert.That(above.Count, Is.EqualTo(2L));
        Assert.That(above.Color, Is.EqualTo(ColorMaps.ViridisStops[^1]));
        Assert.That(scale.Rows.Any(r => r.Role == "below"), Is.False);

        var missing = scale.Rows.Single(r => r.Role == "missing");
        Assert.That(missing.Count, Is.EqualTo(1L));

        Assert.That(ctx.Warnings, Has.Count.EqualTo(1));
        Assert.That(ctx.Warnings[0], Does.Contain("2 of 5"));
        Assert.That(ctx.Warnings[0], Does.Contain("'meshVolume'"));
        Assert.That(ctx.Warnings[0], Does.Contain("(0 below, 2 above)"));
    }

    [Test]
    public void Categorical_Scale_Orders_By_Text_And_Counts_Missing()
    {
        var table = NodeTestHelpers.Table(
            ("kind", typeof(string), ["Wall", "Door", "Wall", null])).Table;

        var scale = ColorScale.Build(table, table.ColumnIndex("kind"), "category10",
            auto: true, min: 0, max: 1, NodeTestHelpers.Ctx, "test.kind");

        Assert.That(scale.Domain, Is.EqualTo("categorical"));
        Assert.That(scale.Rows.Select(r => (r.Role, r.Label, r.Count)), Is.EqualTo(new[]
        {
            ("category", "Door", (long?)1),
            ("category", "Wall", (long?)2),
            ("missing", "no value", (long?)1),
        }));
        Assert.That(scale.Rows[0].Color, Is.EqualTo(ColorMaps.Category10[0]));
        Assert.That(scale.Rows[1].Color, Is.EqualTo(ColorMaps.Category10[1]));
        Assert.That(scale.Rows[2].Color, Is.EqualTo(ColorScale.NoValue));
    }

    [Test]
    public void Gradient_ColorMap_Over_NonNumeric_Column_Falls_Back_To_Category10_With_Warning()
    {
        var table = NodeTestHelpers.Table(
            ("kind", typeof(string), ["Wall", "Door"])).Table;
        var ctx = new FakeEvalContext();

        var scale = ColorScale.Build(table, table.ColumnIndex("kind"), "viridis",
            auto: true, min: 0, max: 1, ctx, "test.kind");

        Assert.That(scale.Domain, Is.EqualTo("categorical"));
        Assert.That(ctx.Warnings, Has.Count.EqualTo(1));
        Assert.That(ctx.Warnings[0], Does.Contain("category10"));
    }

    [Test]
    public void Manual_MinGreaterOrEqualMax_Falls_Back_To_Auto_With_Warning()
    {
        var table = NodeTestHelpers.Table(("value", typeof(double), [1.0, 2.0, 3.0])).Table;
        var ctx = new FakeEvalContext();

        var scale = ColorScale.Build(table, table.ColumnIndex("value"), "viridis",
            auto: false, min: 5, max: 5, ctx, "test.kind");

        Assert.That(scale.Domain, Is.EqualTo("auto"));
        Assert.That(ctx.Warnings, Has.Count.EqualTo(1));
        var stops = scale.Rows.Where(r => r.Role == "stop").ToList();
        Assert.That(stops.First().Value, Is.EqualTo(1.0));
        Assert.That(stops.Last().Value, Is.EqualTo(3.0));
    }

    [Test]
    public void Degenerate_Domain_Gives_One_Stop_At_Midpoint_Color()
    {
        var table = NodeTestHelpers.Table(("value", typeof(double), [4.0, 4.0])).Table;

        var scale = ColorScale.Build(table, table.ColumnIndex("value"), "viridis",
            auto: true, min: 0, max: 1, NodeTestHelpers.Ctx, "test.kind");

        var stops = scale.Rows.Where(r => r.Role == "stop").ToList();
        Assert.That(stops, Has.Count.EqualTo(1));
        Assert.That(stops[0].Value, Is.EqualTo(4.0));
        Assert.That(stops[0].Color, Is.EqualTo(ColorMaps.Gradient(ColorMaps.ViridisStops, 0.5)));
    }

    [Test]
    public void ColorOf_Matches_ColorMaps_Gradient_Within_Tolerance()
    {
        var table = NodeTestHelpers.Table(("value", typeof(double), [0.0, 10.0])).Table;
        var scale = ColorScale.Build(table, table.ColumnIndex("value"), "viridis",
            auto: false, min: 0, max: 10, NodeTestHelpers.Ctx, "test.kind");

        for (var i = 0; i < 20; i++)
        {
            var t = i / 19.0;
            var actual = scale.ColorOf(t * 10.0);
            var expected = ColorMaps.Gradient(ColorMaps.ViridisStops, t);
            Assert.That(actual.R, Is.EqualTo(expected.R).Within(1e-12));
            Assert.That(actual.G, Is.EqualTo(expected.G).Within(1e-12));
            Assert.That(actual.B, Is.EqualTo(expected.B).Within(1e-12));
        }

        Assert.That(scale.ColorOf(null), Is.EqualTo(ColorScale.NoValue));
    }

    [Test]
    public void ColorOf_Categorical_Matches_By_Cell_Text()
    {
        var table = NodeTestHelpers.Table(
            ("kind", typeof(string), ["Wall", "Door", "Wall", null])).Table;
        var scale = ColorScale.Build(table, table.ColumnIndex("kind"), "category10",
            auto: true, min: 0, max: 1, NodeTestHelpers.Ctx, "test.kind");

        Assert.That(scale.ColorOf("Wall"), Is.EqualTo(ColorMaps.Category10[1]));
        Assert.That(scale.ColorOf("Door"), Is.EqualTo(ColorMaps.Category10[0]));
        Assert.That(scale.ColorOf(null), Is.EqualTo(ColorScale.NoValue));
        Assert.That(scale.ColorOf("Roof"), Is.EqualTo(ColorScale.NoValue));
    }

    [Test]
    public void Clamped_Counts_Below_And_Above_The_Domain()
    {
        var table = NodeTestHelpers.Table(("value", typeof(double), [0.0, 10.0])).Table;
        var scale = ColorScale.Build(table, table.ColumnIndex("value"), "viridis",
            auto: false, min: 0, max: 10, NodeTestHelpers.Ctx, "test.kind");

        var (below, above) = scale.Clamped([-5.0, -1.0, 5.0, 11.0, 20.0, null]);
        Assert.That(below, Is.EqualTo(2L));
        Assert.That(above, Is.EqualTo(2L));
    }

    [Test]
    public void Clamped_Is_Zero_For_A_Categorical_Scale()
    {
        var table = NodeTestHelpers.Table(("kind", typeof(string), ["Wall", "Door"])).Table;
        var scale = ColorScale.Build(table, table.ColumnIndex("kind"), "category10",
            auto: true, min: 0, max: 1, NodeTestHelpers.Ctx, "test.kind");

        var (below, above) = scale.Clamped(["Wall", "Door", null]);
        Assert.That(below, Is.EqualTo(0L));
        Assert.That(above, Is.EqualTo(0L));
    }

    [Test]
    public void ToTable_Then_FromTable_Round_Trips_Row_For_Row()
    {
        var table = NodeTestHelpers.Table(
            ("meshVolume", typeof(double), [0.2, 0.5, 1.4, 3.0, null])).Table;
        var scale = ColorScale.Build(table, table.ColumnIndex("meshVolume"), "viridis",
            auto: false, min: 0, max: 1, NodeTestHelpers.Ctx, "test.kind");

        var round = ColorScale.FromTable(scale.ToTable(), NodeTestHelpers.Ctx, "test.kind");

        Assert.That(round, Is.Not.Null);
        Assert.That(round!.Column, Is.EqualTo(scale.Column));
        Assert.That(round.Domain, Is.EqualTo(scale.Domain));
        Assert.That(round.Rows, Is.EqualTo(scale.Rows));
    }

    [Test]
    public void FromTable_Missing_Column_Warns_And_Returns_Null()
    {
        var table = NodeTestHelpers.Table(
            ("column", typeof(string), ["v"]),
            ("domain", typeof(string), ["auto"]),
            ("role", typeof(string), ["stop"]),
            ("label", typeof(string), ["0"]),
            ("value", typeof(double), [0.0]),
            ("r", typeof(double), [0.0]),
            ("g", typeof(double), [0.0])).Table; // missing b and count

        var ctx = new FakeEvalContext();
        var scale = ColorScale.FromTable(table, ctx, "test.kind");

        Assert.That(scale, Is.Null);
        Assert.That(ctx.Warnings, Has.Count.EqualTo(1));
        Assert.That(ctx.Warnings[0], Does.Contain("'b'"));
    }

    [Test]
    public void EmptyTable_Has_The_Legend_Schema_With_No_Rows()
    {
        var table = ColorScale.EmptyTable();

        Assert.That(table.Name, Is.EqualTo(ColorScale.TableName));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[]
            { "column", "domain", "role", "label", "value", "r", "g", "b", "count" }));
        Assert.That(table.RowCount(), Is.EqualTo(0));
    }
}
