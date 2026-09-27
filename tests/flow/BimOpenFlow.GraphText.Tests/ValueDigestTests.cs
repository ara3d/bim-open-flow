using Ara3D.DataTable;

namespace BimOpenFlow.GraphText.Tests;

/// <summary>What each value kind prints: scalars as values, tables as shape, columns, first
/// rows, and a content hash, relations through the reader or as their plan.</summary>
[TestFixture]
public sealed class ValueDigestTests
{
    private static readonly TableValue Doors = DoorsWithWidth(0.8123456);

    private static string Print(params FlowValue[] values)
        => Fixtures.PrintSource(null, values);

    [TestCase(true, "Boolean true")]
    [TestCase(42L, "Integer 42")]
    [TestCase(3.14159265, "Number 3.142")]
    [TestCase(-0.0, "Number 0")]
    [TestCase("a \"b\"", "Text \"a \\\"b\\\"\"")]
    public void Scalars_PrintTheirValue(object value, string expected)
        => Assert.That(Print(value switch
        {
            bool b => new BooleanValue(b),
            long i => new IntegerValue(i),
            double d => new NumberValue(d),
            _ => new TextValue((string)value),
        }), Does.Contain("// Ok  " + expected + "\n"));

    [Test]
    public void Table_PrintsShapeColumnsFirstRowsAndHash()
    {
        var text = Print(Doors);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Match(@"// Ok  table 4 rows x 3 cols  sha [0-9a-f]{12}\n"));
            Assert.That(text, Does.Contain("//   columns mark:Text, width:Number, storey:Integer\n"));
            Assert.That(text, Does.Contain("//   [0] \"D1\", 0.8123, 1\n"));
            Assert.That(text, Does.Contain("//   [2] \"D3\", null, 2\n"));
            Assert.That(text, Does.Not.Contain("[3]"), "three sample rows by default");
        });
    }

    private static TableValue DoorsWithWidth(double width)
        => Fixtures.Table(
            ("mark", typeof(string), ["D1", "D2", "D3", "D4"]),
            ("width", typeof(double), [width, 0.9, null, 1234567.0]),
            ("storey", typeof(long), [1L, 1L, 2L, 3L]));

    [Test]
    public void TableHash_IsTheSameForEqualRows_AndChangesBelowThePrintedPrecision()
    {
        var hash = TableText.ContentHash(Doors.Table, GraphTextOptions.Golden);
        Assert.Multiple(() =>
        {
            Assert.That(TableText.ContentHash(DoorsWithWidth(0.8123456).Table, GraphTextOptions.Golden), Is.EqualTo(hash));
            Assert.That(TableText.ContentHash(DoorsWithWidth(0.8123457).Table, GraphTextOptions.Golden), Is.Not.EqualTo(hash));
        });
    }

    [Test]
    public void TableHash_IgnoresTheMachineSpecificPartOfAliasedPaths()
    {
        static IDataTable PathTable(string root)
            => Fixtures.Table(("file", typeof(string), [root + "/model.ifc"])).Table;
        string Hash(string root)
            => TableText.ContentHash(PathTable(root), GraphTextOptions.Golden with { PathAliases = [new(root, "{SAMPLES}")] });
        Assert.That(Hash("C:/one/samples"), Is.EqualTo(Hash("D:/two/elsewhere/samples")));
    }

    [Test]
    public void WideTables_CapTheColumnsListed()
    {
        var wide = Fixtures.Table(Enumerable.Range(0, 20)
            .Select(i => ($"c{i}", typeof(long), new object?[] { (long)i }))
            .ToArray());
        var text = Print(wide);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("c15:Integer (+4 more)\n"));
            Assert.That(text, Does.Contain("[0] 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, ...\n"));
        });
    }

    [Test]
    public void MultipleOutputs_PrintOneBlockPerPort()
    {
        var text = Print(new IntegerValue(1), Doors);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("  // Ok\n  //   out0: Integer 1\n  //   out1: table 4 rows x 3 cols"));
            Assert.That(text, Does.Contain("  //     columns mark:Text"));
        });
    }

    private sealed class FakeReader(IDataTable rows) : IRelationReader
    {
        public long Count(RelationValue relation)
            => rows.Rows.Count;

        public IDataTable Rows(RelationValue relation, long limit)
            => limit >= rows.Rows.Count ? rows : Fixtures.Table(("mark", typeof(string), ["D1"])).Table;
    }

    private sealed class FailingReader : IRelationReader
    {
        public long Count(RelationValue relation)
            => throw new InvalidOperationException("source 'nrc' is still being built");

        public IDataTable Rows(RelationValue relation, long limit)
            => throw new InvalidOperationException("unreachable");
    }

    private static readonly RelationValue Plan = new("read_csv(nrc, doors.csv)", "abcdef0123456789abcdef");

    [Test]
    public void Relation_WithoutAReader_PrintsItsPlanOnly()
        => Assert.That(Print(Plan), Does.Contain("// Ok  relation plan abcdef012345\n  //   \"read_csv(nrc, doors.csv)\"\n"));

    [Test]
    public void Relation_WithAReader_IsDigestedLikeATable()
    {
        var text = Fixtures.PrintSource(GraphTextOptions.Golden with { Relations = new FakeReader(Doors.Table) }, Plan);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Match(@"// Ok  relation 4 rows x 3 cols  sha [0-9a-f]{12}  plan abcdef012345\n"));
            Assert.That(text, Does.Contain("[1] \"D2\", 0.9, 1"));
        });
    }

    [Test]
    public void Relation_OverTheCap_IsSampledAndNotHashed()
        => Assert.That(
            Fixtures.PrintSource(GraphTextOptions.Golden with { Relations = new FakeReader(Doors.Table), RelationRowCap = 2 }, Plan),
            Does.Contain("// Ok  relation 4 rows x 1 cols  plan abcdef012345  (over 2, not hashed)\n"));

    [Test]
    public void Relation_WhoseRowsCannotBeRead_SaysWhy()
        => Assert.That(
            Fixtures.PrintSource(GraphTextOptions.Golden with { Relations = new FailingReader() }, Plan),
            Does.Contain("// Ok  relation plan abcdef012345  rows unavailable: source 'nrc' is still being built\n"));
}
