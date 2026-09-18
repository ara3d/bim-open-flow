namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>bfast.read and bfast.buffer against a file written by the SDK writer into a
/// temp folder: the buffer directory, typed reads with widening, and the two errors.</summary>
[TestFixture]
public sealed class BfastNodeTests
{
    private string _dir = "";
    private string _path = "";

    [OneTimeSetUp]
    public void WriteFile()
    {
        _dir = Directory.CreateTempSubdirectory("tables-bfast-").FullName;
        _path = Path.Combine(_dir, "sample.bfast");
        SampleBfast.Write(_path);
    }

    [OneTimeTearDown]
    public void DeleteFile()
        => Directory.Delete(_dir, recursive: true);

    [Test]
    public void Read_ListsBuffersInFileOrder()
    {
        var table = new BfastReadNode().EvalTable([], ("path", _path));
        Assert.Multiple(() =>
        {
            Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "name", "byteLength", "index" }));
            Assert.That(table.ColumnCells("name"), Is.EqualTo(new[] { "orderIds", "quantities", "unitPrices", "productCodes" }));
            Assert.That(table.ColumnCells("byteLength"), Is.EqualTo(new object[] { 32L, 32L, 64L, 25L }));
            Assert.That(table.ColumnCells("index"), Is.EqualTo(new object[] { 0L, 1L, 2L, 3L }));
        });
    }

    [Test]
    public void Buffer_Int32_WidensToLong()
    {
        var table = new BfastBufferNode().EvalTable([], ("path", _path), ("name", "quantities"), ("type", "int32"));
        Assert.Multiple(() =>
        {
            Assert.That(table.Name, Is.EqualTo("quantities"));
            Assert.That(table.ColumnNames(), Is.EqualTo(new[] { "value" }));
            Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(long)));
            Assert.That(table.ColumnCells("value"), Is.EqualTo(SampleBfast.Quantities.Select(q => (object)(long)q)));
        });
    }

    [Test]
    public void Buffer_Float64_ReadsDoubles()
    {
        var table = new BfastBufferNode().EvalTable([], ("path", _path), ("name", "unitPrices"), ("type", "float64"));
        Assert.That(table.Columns[0].Descriptor.Type, Is.EqualTo(typeof(double)));
        Assert.That(table.ColumnCells("value"), Is.EqualTo(SampleBfast.UnitPrices.Cast<object>()));
    }

    [Test]
    public void Buffer_Uint8_ReadsEveryByte()
    {
        var table = new BfastBufferNode().EvalTable([], ("path", _path), ("name", "productCodes"), ("type", "uint8"));
        Assert.That(table.Rows, Has.Count.EqualTo(25));
        Assert.That(table.Cell("value", 0), Is.EqualTo((long)'P'));
        Assert.That(table.Cell("value", 2), Is.EqualTo(0L), "null terminator");
    }

    [Test]
    public void Buffer_UnknownName_ThrowsListingTheBuffers()
        => Assert.That(() => new BfastBufferNode().EvalTable([], ("path", _path), ("name", "nope"), ("type", "int32")),
            Throws.ArgumentException.With.Message.StartsWith("bfast.buffer: ")
                .And.Message.Contains("'nope'").And.Message.Contains("orderIds"));

    [Test]
    public void Buffer_LengthNotAMultipleOfTheElementSize_ThrowsNamingTheBuffer()
        => Assert.That(() => new BfastBufferNode().EvalTable([], ("path", _path), ("name", "productCodes"), ("type", "float64")),
            Throws.ArgumentException.With.Message.Contains("'productCodes'").And.Message.Contains("25 bytes"));

    [Test]
    public void Read_MissingFile_Throws()
        => Assert.That(() => new BfastReadNode().EvalTable([], ("path", Path.Combine(_dir, "missing.bfast"))),
            Throws.TypeOf<FileNotFoundException>());
}
