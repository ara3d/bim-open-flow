using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Ara3D.Ifc.Tests;
using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;
using BimOpenFlow.TestSupport;

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

    // Zones: the Run allocates a zone's STEP id, so zone property sets are addressed by name.

    private const string TwoSpaces = """
        #1=IFCPERSON($,$,'p',$,$,$,$,$);
        #2=IFCORGANIZATION($,'o',$,$,$);
        #3=IFCPERSONANDORGANIZATION(#1,#2,$);
        #4=IFCAPPLICATION(#2,'1','app','app');
        #5=IFCOWNERHISTORY(#3,#4,$,.ADDED.,$,$,$,0);
        #6=IFCWALL('0000000000000000000000',#5,'W',$,$,$,$,$,$);
        #7=IFCSPACE('0000000000000000000007',#5,'S1',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        #8=IFCSPACE('0000000000000000000008',#5,'S2',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        #9=IFCSPACE('0000000000000000000009',#5,'S3',$,$,$,$,$,.ELEMENT.,.INTERNAL.,$);
        """;

    private static IDataTable ZoneRows()
        => new MemoryTable("zones", new[]
        {
            new MemoryColumn("zoneName", typeof(string), new object?[] { "North", "North", "South" }, 0),
            new MemoryColumn("spaceId", typeof(long), new object?[] { 7L, 8L, 9L }, 1),
        });

    private static IDataTable ZonePsetRows()
        => new MemoryTable("zonePsets", new[]
        {
            new MemoryColumn("zoneName", typeof(string), new object?[] { "South", "North", "North" }, 0),
            new MemoryColumn("psetName", typeof(string), new object?[] { "NRC_ZoneSummary", "NRC_ZoneSummary", "NRC_ZoneSummary" }, 1),
            new MemoryColumn("paramName", typeof(string), new object?[] { "SpaceCount", "SpaceCount", "Area" }, 2),
            new MemoryColumn("paramValue", typeof(string), new object?[] { "1", "2", "12.5" }, 3),
            new MemoryColumn("valueType", typeof(string), new object?[] { "Integer", "Integer", "Real" }, 4),
        });

    private FlowValue[] WithZones(IDataTable? zones, IDataTable? zonePsets)
        => new FlowValue[]
        {
            new TableValue(PsetRows()),
            zones == null ? MissingValue.Instance : new TableValue(zones),
            zonePsets == null ? MissingValue.Instance : new TableValue(zonePsets),
        };

    [Test]
    public void ZonesAreAppendedAfterTheEntitySets_WithTheirSetsAttachedByName()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(TwoSpaces));
        var outputs = new WritePsetsNode().Eval(
            FakeContext.Run, WithZones(ZoneRows(), ZonePsetRows()),
            Params(("sourcePath", _sourcePath), ("targetPath", _targetPath)));

        using var source = IfcSourceFile.Load(_sourcePath);
        using var target = IfcSourceFile.Load(_targetPath);
        var diff = IfcDiff.Compare(source, target);
        Assert.That(diff.Deleted, Is.Empty);
        Assert.That(diff.Changed, Is.Empty);

        // Entity sets as before take #10 to #16 (values, set and relationship per group). Then North is
        // #17 with its relationship #18, South #19 with #20, then the zone sets.
        var types = diff.Added.Select(id => target.GetSpan(id)!.Value.TypeName).ToList();
        Assert.That(types.Skip(7).Take(4), Is.EqualTo(new[] { "IFCZONE", "IFCRELASSIGNSTOGROUP", "IFCZONE", "IFCRELASSIGNSTOGROUP" }));
        Assert.That(types.Count(t => t == "IFCZONE"), Is.EqualTo(2));
        var text = File.ReadAllText(_targetPath);
        Assert.That(text, Does.Contain("IFCZONE('"));
        Assert.That(text, Does.Contain(",'North',$,$,$);"));
        Assert.That(text, Does.Contain("(#7,#8),$,#17);"));
        Assert.That(text, Does.Contain("(#9),$,#19);"));
        Assert.That(text, Does.Contain("IFCPROPERTYSINGLEVALUE('SpaceCount',$,IFCINTEGER(2),$)"));
        Assert.That(text, Does.Contain("IFCPROPERTYSINGLEVALUE('Area',$,IFCREAL(12.5),$)"));
        Assert.That(text, Does.Contain("(#17),#"), "North's property set relates to the North zone");

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "zonesWritten"), Is.EqualTo(2L));
        Assert.That(Cell(summary, "entitiesTouched"), Is.EqualTo(3L), "the wall and the two zones");
        Assert.That(Cell(summary, "valuesWritten"), Is.EqualTo(6L));
    }

    [Test]
    public void ZonesDoNotMoveTheIdsOfTheEntitySets()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(TwoSpaces));
        var parameters = Params(("sourcePath", _sourcePath), ("targetPath", _targetPath));
        new WritePsetsNode().Eval(FakeContext.Run, TableInput(PsetRows()), parameters);
        var without = File.ReadAllText(_targetPath);
        new WritePsetsNode().Eval(FakeContext.Run, WithZones(ZoneRows(), ZonePsetRows()), parameters);
        var with = File.ReadAllText(_targetPath);

        var cut = without.LastIndexOf("ENDSEC;", StringComparison.Ordinal);
        Assert.That(cut, Is.GreaterThan(0));
        Assert.That(with, Does.StartWith(without[..cut]));
    }

    [Test]
    public void ZoneWritesAreDeterministic()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(TwoSpaces));
        var parameters = Params(("sourcePath", _sourcePath), ("targetPath", _targetPath));
        var node = new WritePsetsNode();
        node.Eval(FakeContext.Run, WithZones(ZoneRows(), ZonePsetRows()), parameters);
        var first = File.ReadAllBytes(_targetPath);
        node.Eval(FakeContext.Run, WithZones(ZoneRows(), ZonePsetRows()), parameters);
        Assert.That(File.ReadAllBytes(_targetPath), Is.EqualTo(first));
    }

    [Test]
    public void AZoneSetNamingAnUndefinedZoneThrows()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(TwoSpaces));
        var stray = new MemoryTable("zonePsets", new[]
        {
            new MemoryColumn("zoneName", typeof(string), new object?[] { "East" }, 0),
            new MemoryColumn("psetName", typeof(string), new object?[] { "NRC_ZoneSummary" }, 1),
            new MemoryColumn("paramName", typeof(string), new object?[] { "x" }, 2),
            new MemoryColumn("paramValue", typeof(string), new object?[] { "y" }, 3),
        });
        Assert.Throws<ArgumentException>(() =>
            new WritePsetsNode().Eval(FakeContext.Run, WithZones(ZoneRows(), stray),
                Params(("sourcePath", _sourcePath), ("targetPath", _targetPath))));
    }

    [Test]
    public void AZoneNamingAMissingSpaceThrows()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(TwoSpaces));
        var bad = new MemoryTable("zones", new[]
        {
            new MemoryColumn("zoneName", typeof(string), new object?[] { "North" }, 0),
            new MemoryColumn("spaceId", typeof(long), new object?[] { 999L }, 1),
        });
        Assert.Throws<ArgumentException>(() =>
            new WritePsetsNode().Eval(FakeContext.Run, WithZones(bad, null),
                Params(("sourcePath", _sourcePath), ("targetPath", _targetPath))));
    }

    // Document reference: the long table is recorded on the project with its checksum and join key.

    private const string WithProject = """
        #1=IFCPERSON($,$,'p',$,$,$,$,$);
        #2=IFCORGANIZATION($,'o',$,$,$);
        #3=IFCPERSONANDORGANIZATION(#1,#2,$);
        #4=IFCAPPLICATION(#2,'1','app','app');
        #5=IFCOWNERHISTORY(#3,#4,$,.ADDED.,$,$,$,0);
        #6=IFCWALL('0000000000000000000000',#5,'W',$,$,$,$,$,$);
        #7=IFCPROJECT('0000000000000000000007',#5,'P',$,$,$,$,$,$);
        """;

    private string WriteTable(string text)
    {
        var path = Path.Combine(_dir, "long.csv");
        File.WriteAllBytes(path, System.Text.Encoding.ASCII.GetBytes(text));
        return path;
    }

    [Test]
    public void DocumentPath_AddsAReferenceOnTheProject_WithNameFormatChecksumAndJoinKey()
    {
        File.WriteAllText(_sourcePath, MiniIfc.Document(WithProject, MiniIfc.Ifc2x3));
        var table = WriteTable("GlobalId,metric\n1,a\n");
        var outputs = new WritePsetsNode().Eval(
            FakeContext.Run, TableInput(PsetRows()),
            Params(("sourcePath", _sourcePath), ("targetPath", _targetPath), ("documentPath", table),
                ("documentDescription", "Long table.")));

        using var source = IfcSourceFile.Load(_sourcePath);
        using var target = IfcSourceFile.Load(_targetPath);
        var diff = IfcDiff.Compare(source, target);
        Assert.That(diff.Deleted, Is.Empty);
        Assert.That(diff.Changed, Is.Empty);
        var types = diff.Added.Select(id => target.GetSpan(id)!.Value.TypeName).ToList();
        Assert.That(types.TakeLast(3), Is.EqualTo(new[] { "IFCDOCUMENTINFORMATION", "IFCDOCUMENTREFERENCE", "IFCRELASSOCIATESDOCUMENT" }));

        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(table))).ToLowerInvariant();
        var text = File.ReadAllText(_targetPath);
        Assert.That(text, Does.Contain("'long.csv'"));
        Assert.That(text, Does.Contain($"Long table. Format: text/csv. SHA-256: {sha}. Join key: GlobalId."));
        Assert.That(text, Does.Contain("(#7),#"), "the reference is associated with the project");
        Assert.That(Cell(OutputTable(outputs), "documentsWritten"), Is.EqualTo(1L));
    }

    [Test]
    public void NoDocumentPath_WritesNoDocument()
    {
        var outputs = new WritePsetsNode().Eval(
            FakeContext.Run, TableInput(PsetRows()),
            Params(("sourcePath", _sourcePath), ("targetPath", _targetPath)));
        Assert.That(File.ReadAllText(_targetPath), Does.Not.Contain("IFCDOCUMENT"));
        Assert.That(Cell(OutputTable(outputs), "documentsWritten"), Is.EqualTo(0L));
    }

    [Test]
    public void DocumentPath_WithoutAProject_Throws()
        => Assert.Throws<ArgumentException>(() =>
            new WritePsetsNode().Eval(FakeContext.Run, TableInput(PsetRows()),
                Params(("sourcePath", _sourcePath), ("targetPath", _targetPath), ("documentPath", WriteTable("a\n")))));
}
