using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Nodes.Tests;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>The inferred schema of each table node equals the columns of its evaluated output, so a shape
/// rule and an evaluation cannot drift; plus the failures a rule reports.</summary>
[TestFixture]
public sealed class TableSchemaRuleTests
{
    static IDataTable Orders => TablesTestHelpers.Orders();
    static IDataTable Customers => TablesTestHelpers.Customers();

    /// <summary>Customers with a second Id column, so the join has a name collision with Orders.</summary>
    static IDataTable CustomersWithId()
    {
        var builder = new DataTableBuilder("customers");
        builder.AddColumn(new object?[] { "C1", "C2" }, "CustomerId", typeof(string));
        builder.AddColumn(new object?[] { 10.5, 20.5 }, "Id", typeof(double));
        builder.AddColumn(new object?[] { true, false }, "Active", typeof(bool));
        return builder.Build();
    }

    [TestCase("left")]
    [TestCase("inner")]
    [TestCase("full")]
    [TestCase("semi")]
    [TestCase("anti")]
    public void Join_MatchesEvaluation(string mode)
        => SchemaGolden.AssertMatchesEvaluation(new TableJoinNode(), [Orders, Customers], ("aKey", "CustomerId"), ("mode", mode));

    [Test]
    public void Join_SuffixesACollisionWithB()
        => SchemaGolden.AssertMatchesEvaluation(new TableJoinNode(), [Orders, CustomersWithId()], ("aKey", "CustomerId"));

    [Test]
    public void Join_DifferentKeyNames()
    {
        var customers = new DataTableBuilder("c");
        customers.AddColumn(new object?[] { "C1" }, "Code", typeof(string));
        customers.AddColumn(new object?[] { 3L }, "Level", typeof(long));
        SchemaGolden.AssertMatchesEvaluation(new TableJoinNode(), [Orders, customers.Build()], ("aKey", "CustomerId"), ("bKey", "Code"));
    }

    [Test]
    public void Join_MissingKeyNamesIt()
    {
        SchemaGolden.AssertFails(new TableJoinNode(), [Orders, Customers], "No column named 'Nope'", ("aKey", "Nope"));
        SchemaGolden.AssertFails(new TableJoinNode(), [Orders, Customers], "No column named 'Code'", ("aKey", "CustomerId"), ("bKey", "Code"));
    }

    [Test]
    public void Join_RequiresItsKeyParameter()
        => SchemaGolden.AssertFails(new TableJoinNode(), [Orders, Customers], "aKey");

    [Test]
    public void Join_UpstreamFailureIsPassedOn()
    {
        var node = new TableJoinNode();
        var inputs = new[]
        {
            new SchemaInput(PortType.Table, true, SchemaResult.Fail("No column named 'x'.")),
            new SchemaInput(PortType.Table, true, SchemaResult.Of(new Column("a", ColumnType.Text))),
        };
        var result = node.Infer("table", inputs, NodeTestHelpers.Params(("aKey", "a")), new StaticCatalog(new Dictionary<string, Schema>()));
        Assert.That(result.ToString(), Is.EqualTo("No column named 'x'."));
    }

    [Test]
    public void Join_UnconnectedInputNamesThePort()
    {
        var inputs = new[]
        {
            new SchemaInput(PortType.Table, true, SchemaResult.Of(new Column("a", ColumnType.Text))),
            new SchemaInput(PortType.Table, false),
        };
        var result = new TableJoinNode().Infer("table", inputs, NodeTestHelpers.Params(("aKey", "a")), new StaticCatalog(new Dictionary<string, Schema>()));
        Assert.That(result.ToString(), Is.EqualTo("Input 'b' is not connected."));
    }

    [Test]
    public void Project_MatchesEvaluationWithAliasesAndOrder()
        => SchemaGolden.AssertMatchesEvaluation(new TableProjectNode(), [Customers], ("columns", "name as Label, CUSTOMERID"));

    [Test]
    public void Project_UnknownColumnFailsNamingIt()
        => SchemaGolden.AssertFails(new TableProjectNode(), [Customers], "No column named 'Age'", ("columns", "Name, Age"));

    [Test]
    public void Project_DuplicateOutputName()
        => SchemaGolden.AssertFails(new TableProjectNode(), [Customers], "duplicate column 'Name'", ("columns", "CustomerId as Name, Name"));

    [TestCase("union")]
    [TestCase("intersect")]
    [TestCase("subtract")]
    public void SetOp_MatchesEvaluation(string op)
    {
        var other = new DataTableBuilder("orders2");
        other.AddColumn(new object?[] { 9L }, "Id", typeof(long));
        other.AddColumn(new object?[] { "C3" }, "CustomerId", typeof(string));
        SchemaGolden.AssertMatchesEvaluation(new TableSetOpNode(), [Orders, other.Build()], ("op", op), ("key", "CustomerId"));
    }

    [Test]
    public void SetOp_UnionOfDifferentColumnsFails()
        => SchemaGolden.AssertFails(new TableSetOpNode(), [Orders, Customers], "missing from b: [Id]", ("op", "union"), ("key", "CustomerId"));

    [Test]
    public void SetOp_KeyMissingFromB()
        => SchemaGolden.AssertFails(new TableSetOpNode(), [Orders, Customers], "No column named 'Id'", ("op", "intersect"), ("key", "Id"));

    [Test]
    public void Range_ColumnTakesItsNameFromTheParameter()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableRangeNode(), [], ("stop", "3"));
        SchemaGolden.AssertMatchesEvaluation(new TableRangeNode(), [], ("name", "storey"), ("stop", "3"));
    }

    [Test]
    public void Calendar_ColumnTakesItsNameFromTheParameter()
    {
        SchemaGolden.AssertMatchesEvaluation(new TableCalendarNode(), [], ("start", "2026-01-01"), ("end", "2026-01-05"));
        SchemaGolden.AssertMatchesEvaluation(new TableCalendarNode(), [], ("name", "day"), ("start", "2026-01-01"), ("end", "2026-03-01"), ("step", "month"));
    }

    [Test]
    public void Inline_MatchesEvaluation()
        => SchemaGolden.AssertMatchesEvaluation(new TableInlineNode(), [], ("rows",
            """[{"type":"Wall","rate":120,"qty":2,"active":true},{"type":"Door","rate":80.5,"qty":null,"extra":"x"}]"""));

    [Test]
    public void Inline_NullableOnlyWhereACellIsNull()
    {
        var result = SchemaGolden.Infer(new TableInlineNode(), new List<Schema>(), ("rows", """[{"a":1,"b":null},{"a":2}]"""));
        Assert.That(result.Schema!.Columns.Select(c => c.Nullable), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void Inline_MixedTypesAndBadJsonFail()
    {
        SchemaGolden.AssertFails(new TableInlineNode(), [], "mixes value types", ("rows", """[{"a":1},{"a":"x"}]"""));
        SchemaGolden.AssertFails(new TableInlineNode(), [], "not valid JSON", ("rows", "[{"));
    }
}
