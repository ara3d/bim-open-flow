using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

/// <summary>Tables on the wire carry only the five spec value kinds, so the
/// pack's readers must land DuckDB DATE/TIMESTAMP columns as ISO-8601 text
/// (the integration defect Track C found: ValueHash rejects DateOnly).</summary>
[TestFixture]
public sealed class DateNormalizationTests
{
    private string _folder = null!;
    private string _csvPath = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _csvPath = Path.Combine(_folder, "dated.csv");
        File.WriteAllText(_csvPath, "Id,When\n1,2026-01-15\n2,2026-02-03\n");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Test]
    public void DuckRead_DateColumn_LandsAsIsoText()
    {
        var table = ((TableValue)new DuckReadNode().Eval(
            NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(("path", _csvPath)))[0]).Table;
        var when = table.Columns.Single(c => c.Descriptor.Name == "When");
        Assert.That(when.Descriptor.Type, Is.EqualTo(typeof(string)));
        Assert.That(table[when.ColumnIndex, 0], Is.EqualTo("2026-01-15"));
    }

    [Test]
    public void SqlQuery_DateExpression_LandsAsIsoText()
    {
        var input = ((TableValue)new DuckReadNode().Eval(
            NodeTestHelpers.Ctx, [], NodeTestHelpers.Params(("path", _csvPath)))[0]);
        var table = ((TableValue)new SqlQueryNode().Eval(
            NodeTestHelpers.Ctx,
            [input, MissingValue.Instance, MissingValue.Instance, MissingValue.Instance],
            NodeTestHelpers.Params(("sql", "SELECT CAST(NULL AS DATE) AS Empty, DATE '2026-03-01' AS Fixed FROM t")))[0]).Table;
        Assert.That(table[1, 0], Is.EqualTo("2026-03-01"));
        Assert.That(table[0, 0], Is.Null);
    }
}
