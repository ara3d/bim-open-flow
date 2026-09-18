using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Relations.Tests;

/// <summary>rel.fromTable over a table shaped like the bos.load entities output
/// (EntityIndex, StepId, GlobalId, Name, Category, Type), through rel.filter and back to
/// rows through rel.materialize. Nothing here touches a file or a registered source.</summary>
[TestFixture]
public sealed class FromTableNodeTests
{
    private RelationRuntime _runtime = null!;

    [OneTimeSetUp]
    public void Up() => _runtime = new RelationRuntime(ConnectionRegistry.Of());

    /// <summary>Three entities: two walls and a roof.</summary>
    static IDataTable Entities(string roofName = "Roof") =>
        NodeTestHelpers.Table(
            ("EntityIndex", new long[] { 0, 1, 2 }),
            ("StepId", new long[] { 101, 102, 103 }),
            ("GlobalId", new[] { "0iEHWY1$XA8eQeeULq4jpl", "1kGzWY1$XA8eQeeULq4jpm", "0jf0rYHfX3RAB3bSIRjmxl" }),
            ("Name", new[] { "Basic Wall", "Basic Wall", roofName }),
            ("Category", new[] { "IFCWALL", "IFCWALL", "IFCROOF" }),
            ("Type", new[] { "Generic 200", "Generic 200", "Generic Roof" })).Table;

    private RelationValue FromTable(IDataTable table, params (string, string)[] ps)
        => (RelationValue)new RelFromTableNode(_runtime).Eval(NodeTestHelpers.Ctx, [new TableValue(table)],
            NodeTestHelpers.Params(ps))[0];

    private RelationValue Filter(RelationValue input, string expr)
        => (RelationValue)new RelFilterNode(_runtime).Eval(NodeTestHelpers.Ctx, [input],
            NodeTestHelpers.Params(("expr", expr)))[0];

    [Test]
    public void PlanCarriesTheDefaultNameAndTheContentHash()
    {
        var relation = FromTable(Entities());
        Assert.That(relation.Text, Does.StartWith("(inline \"t\" \""));
        Assert.That(((InlineTable)relation.Payload!).TableHash, Has.Length.EqualTo(64));
    }

    [Test]
    public void SchemaComesFromTheColumnTypes()
        => Assert.That(_runtime.Schema((Plan)FromTable(Entities()).Payload!).ToString(),
            Is.EqualTo("EntityIndex:Integer?, StepId:Integer?, GlobalId:Text?, Name:Text?, Category:Text?, Type:Text?"));

    [Test]
    public void TheSameRowsGiveTheSamePlan()
    {
        var first = FromTable(Entities()).Hash;
        Assert.That(FromTable(Entities()).Hash, Is.EqualTo(first));
    }

    [Test]
    public void DifferentRowsGiveADifferentPlan()
        => Assert.That(FromTable(Entities("Flat Roof")).Hash, Is.Not.EqualTo(FromTable(Entities()).Hash));

    [Test]
    public void DifferentNamesGiveADifferentPlan()
        => Assert.That(FromTable(Entities(), ("name", "entities")).Hash, Is.Not.EqualTo(FromTable(Entities()).Hash));

    [Test]
    public void FilterAndMaterializeRoundTripTheRows()
    {
        var walls = Filter(FromTable(Entities(), ("name", "entities")), "[Category] == 'IFCWALL'");
        var table = new RelMaterializeNode(_runtime).EvalTable([walls]);
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "EntityIndex", "StepId", "GlobalId", "Name", "Category", "Type" }));
        Assert.That(table.ColumnCells("StepId"), Is.EqualTo(new object?[] { 101L, 102L }));
    }

    [Test]
    public void CountRunsWithoutMaterializing()
        => Assert.That(_runtime.Count((Plan)Filter(FromTable(Entities()), "[Category] == 'IFCROOF'").Payload!), Is.EqualTo(1));

    [Test]
    public void MissingColumnIsANodeError()
        => Assert.That(() => Filter(FromTable(Entities()), "[Missing] == 1"),
            Throws.ArgumentException.With.Message.Contains("Unknown identifier"));

    [Test]
    public void ANonTableInputIsRejected()
        => Assert.That(() => new RelFromTableNode(_runtime).Eval(NodeTestHelpers.Ctx, [new TextValue("x")], ParamValues.Empty),
            Throws.ArgumentException.With.Message.EqualTo("rel.fromTable: input 0 must be a Table."));

    [Test]
    public void ABlankNameIsRejected()
        => Assert.That(() => FromTable(Entities(), ("name", "  ")),
            Throws.ArgumentException.With.Message.EqualTo("rel.fromTable: parameter 'name' cannot be blank."));
}
