using Ara3D.DataTable;
using Ara3D.Ifc.Tests;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class WritePsetsTests
{
    private string _dir = "";
    private string _sourcePath = "";
    private string _targetPath = "";

    [SetUp]
    public void SetUp()
    {
        _dir = NewTempDir();
        _sourcePath = Path.Combine(_dir, "mini.ifc");
        _targetPath = Path.Combine(_dir, "mini-out.ifc");
        File.WriteAllText(_sourcePath, MiniIfc.Wall);
    }

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    /// <summary>Rows: two values in Pset_A plus one in Pset_B, all on the wall.</summary>
    private static IDataTable PsetRows()
        => new MemoryTable("psets", new[]
        {
            new MemoryColumn("entityId", typeof(long), new object?[] { (long)MiniIfc.WallId, (long)MiniIfc.WallId, (long)MiniIfc.WallId }, 0),
            new MemoryColumn("psetName", typeof(string), new object?[] { "Pset_A", "Pset_A", "Pset_B" }, 1),
            new MemoryColumn("paramName", typeof(string), new object?[] { "FireRating", "Status", "Reviewer" }, 2),
            new MemoryColumn("paramValue", typeof(string), new object?[] { "2HR", "Approved", "C. Diggins" }, 3),
        });

    [Test]
    public void AppendsPsetsByteExactly()
    {
        var outputs = new WritePsetsNode().Eval(
            FakeContext.Run, TableInput(PsetRows()),
            Params(("sourcePath", _sourcePath), ("targetPath", _targetPath)));

        using var source = IfcSourceFile.Load(_sourcePath);
        using var target = IfcSourceFile.Load(_targetPath);

        // 3 property values + 2 psets + 2 rels appended; nothing else touched.
        var diff = IfcDiff.Compare(source, target);
        Assert.That(diff.Added, Has.Count.EqualTo(7));
        Assert.That(diff.Deleted, Is.Empty);
        Assert.That(diff.Changed, Is.Empty);

        var text = File.ReadAllText(_targetPath);
        Assert.That(text, Does.StartWith("ISO-10303-21;"));
        Assert.That(text, Does.Contain("IFCPROPERTYSINGLEVALUE('FireRating',$,IFCTEXT('2HR'),$)"));
        Assert.That(text, Does.Contain("'Pset_A'"));
        Assert.That(text, Does.Contain("'Pset_B'"));
        Assert.That(text, Does.Contain("(#6)"));

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "targetPath"), Is.EqualTo(_targetPath));
        Assert.That(Cell(summary, "entitiesTouched"), Is.EqualTo(1L));
        Assert.That(Cell(summary, "valuesWritten"), Is.EqualTo(3L));
    }

    [Test]
    public void RepeatedRunsAreDeterministic()
    {
        var node = new WritePsetsNode();
        var input = TableInput(PsetRows());
        var parameters = Params(("sourcePath", _sourcePath), ("targetPath", _targetPath));
        node.Eval(FakeContext.Run, input, parameters);
        var first = File.ReadAllBytes(_targetPath);
        node.Eval(FakeContext.Run, input, parameters);
        Assert.That(File.ReadAllBytes(_targetPath), Is.EqualTo(first));
    }

    [Test]
    public void UnknownEntityIdThrows()
    {
        var rows = new MemoryTable("psets", new[]
        {
            new MemoryColumn("entityId", typeof(long), new object?[] { 999L }, 0),
            new MemoryColumn("psetName", typeof(string), new object?[] { "Pset_A" }, 1),
            new MemoryColumn("paramName", typeof(string), new object?[] { "x" }, 2),
            new MemoryColumn("paramValue", typeof(string), new object?[] { "y" }, 3),
        });
        Assert.Throws<ArgumentException>(() =>
            new WritePsetsNode().Eval(FakeContext.Run, TableInput(rows),
                Params(("sourcePath", _sourcePath), ("targetPath", _targetPath))));
    }

    [Test]
    public void MissingColumnThrows()
    {
        var rows = new MemoryTable("psets", new[]
        {
            new MemoryColumn("entityId", typeof(long), new object?[] { (long)MiniIfc.WallId }, 0),
        });
        Assert.Throws<ArgumentException>(() =>
            new WritePsetsNode().Eval(FakeContext.Run, TableInput(rows),
                Params(("sourcePath", _sourcePath), ("targetPath", _targetPath))));
    }
}
