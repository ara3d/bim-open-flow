using Ara3D.DataFlowEngine.TestKit;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// One sql.query over three different sources: customers from CSV via DuckDB,
/// orders from SQLite, products from XLSX. The SQLite and XLSX fixtures are
/// generated into a temp dir through the same code path as the seeder, so this
/// never depends on the [Explicit] seed having run.
/// </summary>
[TestFixture]
public sealed class ThreeSourceCombineTests
{
    private string _dir = null!;

    [OneTimeSetUp]
    public void SeedTempFixtures()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bimopenflow-table-workflows", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        SampleFixtures.SeedAll(SamplePaths.TablesDir, _dir);
    }

    [OneTimeTearDown]
    public void DeleteTempFixtures()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Revenue per customer, by hand from the CSVs:
    /// Acme 120*3.85 + 40*12.5 + 22*240 = 6242; Beaver 300*3.85 + 75*45.75 = 4586.25;
    /// Cedar 15*240 = 3600; Dovetail 90*3.85 = 346.5; Evergreen 60*12.5 = 750.</summary>
    [Test]
    public void CsvSqliteXlsx_CombineIntoRevenuePerCustomer()
    {
        var session = TableReads.NewTableSession();
        session.Evaluate(Graph
            .Node("customers", "duck.read", ("path", SamplePaths.Csv("customers")))
            .Node("orders", "sqlite.query",
                ("path", Path.Combine(_dir, SampleFixtures.SqliteName)),
                ("sql", "SELECT OrderId, CustomerId, ProductId, Quantity FROM Orders"))
            .Node("products", "xlsx.read",
                ("path", Path.Combine(_dir, SampleFixtures.XlsxName)),
                ("sheet", "Products"))
            .Node("revenue", "sql.query", ("sql",
                "SELECT t1.Name AS Customer, CAST(SUM(t2.Quantity * t3.UnitPrice) AS DOUBLE) AS Revenue "
                + "FROM t2 JOIN t1 ON t2.CustomerId = t1.CustomerId "
                + "JOIN t3 ON t2.ProductId = t3.ProductId "
                + "GROUP BY t1.Name ORDER BY t1.Name"))
            .Connect("customers.table", "revenue.t1")
            .Connect("orders.table", "revenue.t2")
            .Connect("products.table", "revenue.t3")
            .Build());

        var revenue = session.Table("revenue");
        Assert.That(revenue.Column("Customer"), Is.EqualTo(new[]
            { "Acme Construction", "Beaver Builds", "Cedar Design", "Dovetail Eng", "Evergreen Dev" }));
        Assert.That(revenue.Column("Revenue"), Is.EqualTo(new[] { 6242.0, 4586.25, 3600.0, 346.5, 750.0 }));
    }
}
