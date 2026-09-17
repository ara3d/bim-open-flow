using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using DuckDB.NET.Data;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class SqlQueryNodeTests
{
    private static IDataTable Csv(string fileName)
        => new DuckReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath(fileName)));

    /// <summary>The inputs list exactly as the engine delivers it: MissingValue
    /// placeholders in unconnected optional positions.</summary>
    private static IReadOnlyList<FlowValue> Inputs(params IDataTable?[] tables)
        => Enumerable.Range(0, 4)
            .Select(i => i < tables.Length && tables[i] != null
                ? (FlowValue)new TableValue(tables[i]!)
                : MissingValue.Instance)
            .ToList();

    [Test]
    public void Query_OneInput_TAliasesT1()
    {
        var table = new SqlQueryNode().EvalTable(Inputs(Csv("customers.csv")),
            ("sql", "SELECT City FROM t WHERE Segment = 'Contractor' ORDER BY City"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "City" }));
        Assert.That(table.Rows, Is.Not.Empty);
    }

    [Test]
    public void Query_TwoInputs_JoinT1T2()
    {
        var table = new SqlQueryNode().EvalTable(Inputs(Csv("orders.csv"), Csv("customers.csv")),
            ("sql", "SELECT t2.Name, count(*) AS Orders FROM t1 JOIN t2 USING (CustomerId) GROUP BY t2.Name ORDER BY t2.Name"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "Orders" }));
        Assert.That(table.Rows, Is.Not.Empty);
    }

    [Test]
    public void Query_ThreeInputs_JoinAcrossAll()
    {
        var table = new SqlQueryNode().EvalTable(
            Inputs(Csv("orders.csv"), Csv("customers.csv"), Csv("products.csv")),
            ("sql", "SELECT t2.Name, t3.ProductName, t1.Quantity FROM t1 "
                + "JOIN t2 USING (CustomerId) JOIN t3 USING (ProductId) ORDER BY t1.OrderId"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Name", "ProductName", "Quantity" }));
        Assert.That(table.Rows, Is.Not.Empty);
    }

    [Test]
    public void Query_GapInConnections_T3WithoutT2()
    {
        var table = new SqlQueryNode().EvalTable(Inputs(Csv("orders.csv"), null, Csv("products.csv")),
            ("sql", "SELECT count(*) AS N FROM t1 JOIN t3 USING (ProductId)"));
        Assert.That(table.Cell("N", 0), Is.Not.EqualTo(0));
    }

    [Test]
    public void Query_UnconnectedT1_Throws()
        => Assert.That(
            () => new SqlQueryNode().EvalTable(Inputs(), ("sql", "SELECT 1")),
            Throws.ArgumentException.With.Message.Contains("sql.query"));

    [Test]
    public void Query_NonSelect_Rejected()
        => Assert.That(
            () => new SqlQueryNode().EvalTable(Inputs(Csv("customers.csv")),
                ("sql", "DELETE FROM t1")),
            Throws.ArgumentException.With.Message.Contains("sql.query"));

    [Test]
    public void Query_MissingSqlParameter_Throws()
        => Assert.That(() => new SqlQueryNode().EvalTable(Inputs(Csv("customers.csv"))),
            Throws.ArgumentException.With.Message.Contains("sql.query"));

    [Test]
    public void Query_BadColumn_Throws()
        => Assert.That(
            () => new SqlQueryNode().EvalTable(Inputs(Csv("customers.csv")),
                ("sql", "SELECT NoSuchColumn FROM t1")),
            Throws.InstanceOf<DuckDBException>());
}
