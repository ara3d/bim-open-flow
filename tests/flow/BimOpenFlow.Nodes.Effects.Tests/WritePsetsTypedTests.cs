using Ara3D.DataTable;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

/// <summary>
/// The optional valueType column of sink.writePsets: every accepted name, its synonyms,
/// the absent-column default, and the two failure messages.
/// </summary>
public sealed class WritePsetsTypedTests
{
    /// <summary>One row per case: the valueType cell, the text in paramValue, and the IFC literal it must produce.</summary>
    private static readonly (string? ValueType, string Text, string Nominal)[] Cases =
    {
        ("Text", "2HR", "IFCTEXT('2HR')"),
        ("Label", "Baseline", "IFCLABEL('Baseline')"),
        ("Identifier", "run-2026-09-17-01", "IFCIDENTIFIER('run-2026-09-17-01')"),
        ("Integer", "42", "IFCINTEGER(42)"),
        ("Number", "615.5", "IFCREAL(615.5)"),
        ("Number", "3", "IFCREAL(3.)"),
        ("Real", "689.4", "IFCREAL(689.4)"),
        ("Boolean", "true", "IFCBOOLEAN(.T.)"),
        ("Boolean", "False", "IFCBOOLEAN(.F.)"),
        ("real", "1.25", "IFCREAL(1.25)"),
        ("bOOLEAN", "true", "IFCBOOLEAN(.T.)"),
        (" Integer ", "7", "IFCINTEGER(7)"),
        (null, "defaulted", "IFCTEXT('defaulted')"),
        ("", "also defaulted", "IFCTEXT('also defaulted')"),
    };

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

    /// <summary>All four columns plus valueType, one row per (name, type, text) triple.</summary>
    private static IDataTable Rows(params (string? ValueType, string Name, string Text)[] rows)
        => new MemoryTable("psets", new[]
        {
            Column("entityId", typeof(long), rows.Select(_ => (object?)(long)MiniIfc.WallId), 0),
            Column("psetName", typeof(string), rows.Select(_ => (object?)"Pset_Typed"), 1),
            Column("paramName", typeof(string), rows.Select(r => (object?)r.Name), 2),
            Column("valueType", typeof(string), rows.Select(r => (object?)r.ValueType), 3),
            Column("paramValue", typeof(string), rows.Select(r => (object?)r.Text), 4),
        });

    private static MemoryColumn Column(string name, Type type, IEnumerable<object?> values, int index)
        => new(name, type, values.ToArray(), index);

    private string Write(IDataTable rows)
    {
        new WritePsetsNode().Eval(
            FakeContext.Run, TableInput(rows),
            Params(("sourcePath", _sourcePath), ("targetPath", _targetPath)));
        return File.ReadAllText(_targetPath);
    }

    [Test]
    public void EachValueTypeWritesItsIfcLiteral()
    {
        var rows = Cases.Select((c, i) => (c.ValueType, Name: $"P{i}", c.Text)).ToArray();
        var text = Write(Rows(rows));

        Assert.Multiple(() =>
        {
            for (var i = 0; i < Cases.Length; i++)
                Assert.That(text, Does.Contain($"IFCPROPERTYSINGLEVALUE('P{i}',$,{Cases[i].Nominal},$)"),
                    $"case {i}: valueType '{Cases[i].ValueType}'");
        });
    }

    [Test]
    public void AbsentValueTypeColumnStillWritesText()
    {
        var rows = new MemoryTable("psets", new[]
        {
            new MemoryColumn("entityId", typeof(long), new object?[] { (long)MiniIfc.WallId }, 0),
            new MemoryColumn("psetName", typeof(string), new object?[] { "Pset_A" }, 1),
            new MemoryColumn("paramName", typeof(string), new object?[] { "FireRating" }, 2),
            new MemoryColumn("paramValue", typeof(string), new object?[] { "2HR" }, 3),
        });
        Assert.That(Write(rows), Does.Contain("IFCPROPERTYSINGLEVALUE('FireRating',$,IFCTEXT('2HR'),$)"));
    }

    [Test]
    public void UnknownValueTypeNamesTheRowAndTheValue()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Write(Rows((null, "Ok", "x"), ("Quantity", "Bad", "3"))));
        Assert.That(ex!.Message, Does.Contain("Row 1").And.Contain("Quantity"));
    }

    [TestCase("Integer", "3.5", "not an Integer")]
    [TestCase("Number", "wide", "not a Number")]
    [TestCase("Boolean", ".T.", "not a Boolean")]
    public void BadLiteralNamesTheRowAndTheValue(string valueType, string text, string expected)
    {
        var ex = Assert.Throws<ArgumentException>(() => Write(Rows((valueType, "Bad", text))));
        Assert.That(ex!.Message, Does.Contain("Row 0").And.Contain(text).And.Contain(expected));
    }
}
