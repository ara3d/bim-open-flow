using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>A Database output pages as its table list, or as its path and hash when no lister travels with it.</summary>
[TestFixture]
public sealed class DatabaseSliceTests
{
    [Test]
    public void DatabaseWithALister_PagesAsItsTableList()
    {
        var builder = new DataTableBuilder("tables");
        builder.AddColumn(new object?[] { "walls", "doors" }, "name", typeof(string));
        var value = new DatabaseValue("a.duckdb", "h", Payload: (Func<IDataTable>)(() => builder.Build()));

        var slice = value.ToSlice("source", 0, 10);

        Assert.That(slice.Columns.Select(c => c.Name), Is.EqualTo(new[] { "name" }));
        Assert.That(slice.TotalRows, Is.EqualTo(2));
    }

    [Test]
    public void DatabaseReadBackFromARunRecord_PagesAsPathAndHash()
    {
        var slice = new DatabaseValue("a.duckdb", "h").ToSlice("source", 0, 10);

        Assert.That(slice.Columns.Select(c => c.Name), Is.EqualTo(new[] { "path", "hash" }));
        Assert.That(slice.Rows.Single(), Is.EqualTo(new object[] { "a.duckdb", "h" }));
    }
}
