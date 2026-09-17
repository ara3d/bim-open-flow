namespace BimOpenFlow.Nodes.DuckDb.Tests;

[TestFixture]
public sealed class CsvReadNodeTests
{
    private string _folder = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
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

    private string WriteCsv(string name, string content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Test]
    public void Read_Defaults()
    {
        var table = new CsvReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath("customers.csv")));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "CustomerId", "Name", "City", "Segment" }));
        Assert.That(table.Cell("CustomerId", 0), Is.EqualTo("C001"));
    }

    [Test]
    public void Read_Glob_UnionsFilesAndAppendsFilename()
    {
        var glob = Path.Combine(Path.GetDirectoryName(NodeTestHelpers.SamplePath("monthly-2024-01.csv"))!,
            "monthly-*.csv");
        var table = new CsvReadNode().EvalTable([], ("path", glob));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Month", "Item", "Amount", "filename" }));
        Assert.That(table.Rows, Has.Count.EqualTo(6));
        var months = Enumerable.Range(0, 6).Select(r => table.Cell("Month", r)).Distinct().ToList();
        Assert.That(months, Is.EquivalentTo(new[] { "2024-01", "2024-02" }));
        Assert.That(table.Cell("filename", 0)?.ToString(), Does.Contain("monthly-2024-01.csv"));
    }

    [Test]
    public void Read_SkipRows_SkipsJunkAboveHeader()
    {
        var path = WriteCsv("skip.csv", "Report Export\nGenerated 2024-01-01\nA,B\n1,2\n3,4\n");
        var table = new CsvReadNode().EvalTable([], ("path", path), ("skipRows", "2"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void Read_NoHeader_NamesColumns1ToN()
    {
        var path = WriteCsv("noheader.csv", "x,1\ny,2\n");
        var table = new CsvReadNode().EvalTable([], ("path", path), ("header", "false"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "Column1", "Column2" }));
        Assert.That(table.Rows, Has.Count.EqualTo(2));
        Assert.That(table.Cell("Column1", 0), Is.EqualTo("x"));
    }

    [Test]
    public void Read_Delimiter_Semicolon()
    {
        var path = WriteCsv("semi.csv", "A;B\n1;two\n");
        var table = new CsvReadNode().EvalTable([], ("path", path), ("delimiter", ";"));
        Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "A", "B" }));
        Assert.That(table.Cell("B", 0), Is.EqualTo("two"));
    }

    [Test]
    public void Read_InferTypesFalse_EveryColumnIsText()
    {
        var path = WriteCsv("typed.csv", "N,S\n1,alpha\n2,beta\n");
        var table = new CsvReadNode().EvalTable([], ("path", path), ("inferTypes", "false"));
        Assert.That(table.Columns.Select(c => c.Descriptor.Type), Is.All.EqualTo(typeof(string)));
        Assert.That(table.Cell("N", 0), Is.EqualTo("1"));
    }

    [Test]
    public void Read_NullText_TurnsSentinelIntoNull()
    {
        var path = WriteCsv("nulls.csv", "A,B\nN/A,1\nx,2\n");
        var table = new CsvReadNode().EvalTable([], ("path", path), ("nullText", "N/A"));
        Assert.That(table.Cell("A", 0), Is.Null);
        Assert.That(table.Cell("A", 1), Is.EqualTo("x"));
    }

    [Test]
    public void Read_SameContent_ReturnsSameTableInstance()
    {
        var path = WriteCsv("cached.csv", "A,B\n1,2\n");
        var first = new CsvReadNode().EvalTable([], ("path", path));
        var second = new CsvReadNode().EvalTable([], ("path", path));
        Assert.That(ReferenceEquals(first, second), Is.True);
    }

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(
            () => new CsvReadNode().EvalTable([], ("path", Path.Combine(_folder, "absent.csv"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("csv.read"));

    [Test]
    public void Read_EmptyGlob_Throws()
        => Assert.That(
            () => new CsvReadNode().EvalTable([], ("path", Path.Combine(_folder, "nothing-*.csv"))),
            Throws.InstanceOf<FileNotFoundException>().With.Message.Contains("csv.read"));

    [Test]
    public void Read_UnknownEncoding_Throws()
        => Assert.That(
            () => new CsvReadNode().EvalTable([],
                ("path", NodeTestHelpers.SamplePath("customers.csv")), ("encoding", "ebcdic")),
            Throws.ArgumentException.With.Message.Contains("csv.read"));

    [Test]
    public void Read_MissingPathParameter_Throws()
        => Assert.That(() => new CsvReadNode().EvalTable([]),
            Throws.ArgumentException.With.Message.Contains("csv.read"));
}
