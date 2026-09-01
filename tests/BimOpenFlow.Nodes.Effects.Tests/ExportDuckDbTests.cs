using Ara3D.BimOpenSchema.DuckDb;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportDuckDbTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    private string DbPath
        => Path.Combine(_dir, "out.duckdb");

    private void Export(string mode, string table = "facts")
        => new ExportDuckDbNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", DbPath), ("table", table), ("mode", mode)));

    private long CountRows(string table = "facts")
    {
        using var conn = BosDuckDb.Open(DbPath);
        return conn.ScalarInt64($"SELECT count(*) FROM \"{table}\"");
    }

    [Test]
    public void ReplaceRoundTripsValuesAndNulls()
    {
        var outputs = new ExportDuckDbNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", DbPath), ("table", "facts")));

        using var conn = BosDuckDb.Open(DbPath);
        var table = conn.Query("SELECT * FROM facts ORDER BY count NULLS LAST");
        Assert.That(table.Columns.Select(c => c.Descriptor.Name), Is.EqualTo(new[] { "name", "count", "ratio", "flag" }));
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        Assert.That(Cell(table, "name"), Is.EqualTo("plain"));
        Assert.That(Cell(table, "count"), Is.EqualTo(1L));
        Assert.That(Cell(table, "ratio"), Is.EqualTo(0.5));
        Assert.That(Cell(table, "flag"), Is.EqualTo(1L), "booleans land as BIGINT 1/0");
        Assert.That(Cell(table, "count", 2), Is.Null);

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "table"), Is.EqualTo("facts"));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void DeclaredColumnTypesFollowTheMapping()
    {
        Export("replace");
        using var conn = BosDuckDb.Open(DbPath);
        var types = conn.Query(
            "SELECT data_type FROM information_schema.columns WHERE table_name = 'facts' ORDER BY ordinal_position");
        var names = new List<string?>();
        for (var r = 0; r < types.Rows.Count; r++)
            names.Add(types[0, r]?.ToString());
        Assert.That(names, Is.EqualTo(new[] { "VARCHAR", "BIGINT", "DOUBLE", "BIGINT" }));
    }

    [Test]
    public void ReplaceTwiceKeepsThreeRowsAndOtherTables()
    {
        Export("replace");
        Export("replace", "other");
        Export("replace");
        Assert.That(CountRows(), Is.EqualTo(3));
        Assert.That(CountRows("other"), Is.EqualTo(3), "replacing one table leaves the rest of the database alone");
    }

    [Test]
    public void AppendAddsRowsToCompatibleTable()
    {
        Export("replace");
        Export("append");
        Assert.That(CountRows(), Is.EqualTo(6));
    }

    [Test]
    public void AppendCreatesTableWhenAbsent()
    {
        Export("append");
        Assert.That(CountRows(), Is.EqualTo(3));
    }

    [Test]
    public void AppendIncompatibleColumnsThrowsWithKindPrefix()
    {
        using (var conn = BosDuckDb.Open(DbPath))
            conn.Execute("CREATE TABLE facts (other VARCHAR)");
        Assert.That(
            Assert.Throws<ArgumentException>(() => Export("append"))!.Message,
            Does.StartWith("sink.exportDuckDb: "));
    }

    [Test]
    public void FailIfExistsThrowsOnSecondRunWithKindPrefix()
    {
        Export("failIfExists");
        var message = Assert.Throws<InvalidOperationException>(() => Export("failIfExists"))!.Message;
        Assert.That(message, Does.StartWith("sink.exportDuckDb: "));
        Assert.That(message, Does.Contain("already exists"));
        Assert.That(CountRows(), Is.EqualTo(3), "first write is intact");
    }

    [Test]
    public void MissingTableNameThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportDuckDbNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", DbPath))))!.Message,
            Does.StartWith("sink.exportDuckDb: "));
}
