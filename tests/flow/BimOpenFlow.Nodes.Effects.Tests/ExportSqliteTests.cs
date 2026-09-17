using BimOpenFlow.Nodes.Effects;
using Microsoft.Data.Sqlite;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ExportSqliteTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    private string DbPath
        => Path.Combine(_dir, "out.sqlite");

    private static IReadOnlyList<object?[]> ReadRows(string path, string sql)
    {
        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
                row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    private void Export(string mode, string table = "facts")
        => new ExportSqliteNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", DbPath), ("table", table), ("mode", mode)));

    [Test]
    public void ReplaceRoundTripsValuesAndNulls()
    {
        var outputs = new ExportSqliteNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", DbPath), ("table", "facts")));

        var rows = ReadRows(DbPath, "SELECT name, count, ratio, flag FROM facts ORDER BY rowid");
        Assert.That(rows.Count, Is.EqualTo(3));
        Assert.That(rows[0], Is.EqualTo(new object?[] { "plain", 1L, 0.5, 1L }));
        Assert.That(rows[1], Is.EqualTo(new object?[] { "with, comma", null, 2.25, 0L }));
        Assert.That(rows[2][1], Is.EqualTo(3L));
        Assert.That(rows[2][3], Is.Null);

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "table"), Is.EqualTo("facts"));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }

    [Test]
    public void DeclaredColumnTypesFollowTheMapping()
    {
        Export("replace");
        var columns = ReadRows(DbPath, "SELECT name, type FROM pragma_table_info('facts') ORDER BY cid");
        Assert.That(columns.Select(c => (string?)c[1]), Is.EqualTo(new[] { "TEXT", "INTEGER", "REAL", "INTEGER" }));
    }

    [Test]
    public void ReplaceTwiceKeepsThreeRows()
    {
        Export("replace");
        Export("replace");
        Assert.That(ReadRows(DbPath, "SELECT count(*) FROM facts")[0][0], Is.EqualTo(3L));
    }

    [Test]
    public void AppendAddsRowsToCompatibleTable()
    {
        Export("replace");
        Export("append");
        Assert.That(ReadRows(DbPath, "SELECT count(*) FROM facts")[0][0], Is.EqualTo(6L));
    }

    [Test]
    public void AppendCreatesTableWhenAbsent()
    {
        Export("append");
        Assert.That(ReadRows(DbPath, "SELECT count(*) FROM facts")[0][0], Is.EqualTo(3L));
    }

    [Test]
    public void AppendIncompatibleColumnsThrowsWithKindPrefix()
    {
        using (var conn = new SqliteConnection($"Data Source={DbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE TABLE facts (other TEXT)";
            cmd.ExecuteNonQuery();
        }
        Assert.That(
            Assert.Throws<ArgumentException>(() => Export("append"))!.Message,
            Does.StartWith("sink.exportSqlite: "));
    }

    [Test]
    public void FailIfExistsThrowsOnSecondRunWithKindPrefix()
    {
        Export("failIfExists");
        var message = Assert.Throws<InvalidOperationException>(() => Export("failIfExists"))!.Message;
        Assert.That(message, Does.StartWith("sink.exportSqlite: "));
        Assert.That(message, Does.Contain("already exists"));
        Assert.That(ReadRows(DbPath, "SELECT count(*) FROM facts")[0][0], Is.EqualTo(3L), "first write is intact");
    }

    [Test]
    public void MissingTableNameThrowsWithKindPrefix()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                new ExportSqliteNode().Eval(FakeContext.Run, TableInput(FixtureTable()),
                    Params(("path", DbPath))))!.Message,
            Does.StartWith("sink.exportSqlite: "));
}
