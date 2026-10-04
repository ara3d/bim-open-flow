using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// End-to-end table workflows over the committed sample CSVs, evaluated through
/// the "tables" profile registry. Expected numbers are computed by hand from the
/// CSVs (samples/tables/README.md).
/// </summary>
[TestFixture]
public sealed class TableWorkflowTests
{
    /// <summary>orders x products joined in SQL, revenue summed per product.
    /// By hand: P-10 (120+300+90)*3.85=1963.5, P-11 (40+60)*12.5=1250,
    /// P-12 (15+22)*240=8880, P-13 75*45.75=3431.25.</summary>
    [Test]
    public void CsvJoinAggregate_ComputesRevenuePerProduct()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(Graph
            .Node("orders", "duck.read", ("path", SamplePaths.Csv("orders")))
            .Node("products", "duck.read", ("path", SamplePaths.Csv("products")))
            .Node("join", "sql.query", ("sql",
                "SELECT t1.ProductId AS ProductId, t1.Quantity * t2.UnitPrice AS Revenue "
                + "FROM t1 JOIN t2 ON t1.ProductId = t2.ProductId"))
            .Node("revenue", "table.aggregate",
                ("groupBy", "ProductId"), ("aggregates", "sum(Revenue) as TotalRevenue"))
            .Connect("orders.table", "join.t1")
            .Connect("products.table", "join.t2")
            .Connect("join.table", "revenue.table")
            .Build());

        var revenue = session.Table("revenue");
        Assert.That(revenue.Column("ProductId"), Is.EqualTo(new[] { "P-10", "P-11", "P-12", "P-13" }));
        Assert.That(revenue.Column("TotalRevenue"), Is.EqualTo(new[] { 1963.5, 1250.0, 8880.0, 3431.25 }));
    }

    /// <summary>sql.query with only t1 connected: the t alias works.</summary>
    [Test]
    public void SqlQuery_SingleInput_TAliasWorks()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(Graph
            .Node("orders", "duck.read", ("path", SamplePaths.Csv("orders")))
            .Node("big", "sql.query", ("sql",
                "SELECT OrderId FROM t WHERE Quantity > 100 ORDER BY OrderId"))
            .Connect("orders.table", "big.t1")
            .Build());

        Assert.That(session.Table("big").Column("OrderId"), Is.EqualTo(new[] { "O-1001", "O-1003" }));
    }

    /// <summary>table.join left: all 8 order rows survive even though only the
    /// UnitPrice > 10 products (P-11/P-12/P-13) match.</summary>
    [Test]
    public void TableJoin_Left_KeepsAllOrderRows()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(JoinGraph("left"));

        var joined = session.Table("join");
        Assert.That(joined.Rows, Has.Count.EqualTo(8));
        Assert.That(joined.Cell("UnitPrice", 1), Is.EqualTo(12.5), "O-1002 buys P-11 at 12.50");
    }

    /// <summary>table.join inner: only the 5 orders of a UnitPrice > 10 product survive
    /// (O-1002, O-1004, O-1005, O-1006, O-1007).</summary>
    [Test]
    public void TableJoin_Inner_KeepsOnlyMatches()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(JoinGraph("inner"));

        var joined = session.Table("join");
        Assert.That(joined.Rows, Has.Count.EqualTo(5));
        Assert.That(joined.Column("UnitPrice"), Has.All.GreaterThan(10.0));
    }

    /// <summary>table.setOp subtract: removing the Quantity > 100 orders
    /// (O-1001, O-1003) keeps the other 6, in a's row order.</summary>
    [Test]
    public void TableSetOp_Subtract_RemovesLargeOrders()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(Graph
            .Node("orders", "duck.read", ("path", SamplePaths.Csv("orders")))
            .Node("large", "table.filter", ("expr", "Quantity > 100"))
            .Node("small", "table.setOp", ("op", "subtract"), ("key", "OrderId"))
            .Connect("orders.table", "large.table")
            .Connect("orders.table", "small.a")
            .Connect("large.table", "small.b")
            .Build());

        var small = session.Table("small");
        Assert.That(small.Rows, Has.Count.EqualTo(6));
        Assert.That(small.Cell("OrderId", 0), Is.EqualTo("O-1002"));
    }

    /// <summary>orders (a) joined to the UnitPrice > 10 products (b) by ProductId.</summary>
    private static Ara3D.NodeGraph.GraphDocument JoinGraph(string mode)
        => Graph
            .Node("orders", "duck.read", ("path", SamplePaths.Csv("orders")))
            .Node("products", "duck.read", ("path", SamplePaths.Csv("products")))
            .Node("pricey", "table.filter", ("expr", "UnitPrice > 10"))
            .Node("join", "table.join", ("aKey", "ProductId"), ("mode", mode))
            .Connect("products.table", "pricey.table")
            .Connect("orders.table", "join.a")
            .Connect("pricey.table", "join.b")
            .Build();
}
