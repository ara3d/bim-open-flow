using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// The sample graphs over public buildings (samples/buildings) validate against the tables
/// profile, evaluate with every node Ok, and produce the numbers samples/buildings/README.md
/// lists. Without bim-open-data's samples/public in deps/ every test is skipped, with the
/// reason. The numbers were measured on 2026-10-03 and agree with bim-open-data's
/// samples.json where it counts the same thing.
/// </summary>
[TestFixture]
public sealed class PublicBuildingsTests
{
    public static IEnumerable<TestCaseData> GraphIds
        => PublicBuildings.Graphs.Select(g => new TestCaseData(g.Id).SetArgDisplayNames(g.Id));

    [SetUp]
    public void RequireData()
    {
        if (PublicBuildings.MissingDataReason is { } reason)
            Assert.Ignore(reason);
    }

    [Test]
    public void IndexListsEveryGraphFile()
        => Assert.That(
            PublicBuildings.Graphs.Select(g => g.Id).Order(StringComparer.Ordinal),
            Is.EqualTo(Directory.EnumerateFiles(PublicBuildings.GraphsDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => id != "index")
                .Order(StringComparer.Ordinal)));

    [TestCaseSource(nameof(GraphIds))]
    public void ParsesAndValidates(string id)
    {
        Assert.That(AnalysisId.IsValid(id), id);
        Assert.That(PublicBuildings.Load(id).Validate(HostComposition.TablePacks()), Is.Empty);
    }

    [TestCaseSource(nameof(GraphIds))]
    public void Evaluates_EveryNodeOk(string id)
    {
        var session = PublicBuildings.Evaluate(id);
        var failed = session.Snapshot.Results
            .Where(r => r.Value.Status != NodeStatus.Ok)
            .Select(r => $"{r.Key}: {r.Value.Status} {r.Value.Error}")
            .ToList();
        Assert.That(failed, Is.Empty);
    }

    [Test]
    public void Schependomlaan_ElementsByCategory()
    {
        var chart = PublicBuildings.Evaluate("schependomlaan-elements-by-category").Table("chart");
        Assert.That(chart.Rows.Count, Is.EqualTo(16));
        Assert.That(Total(chart, "Elements"), Is.EqualTo(3721));
        Assert.That(Lookup(chart, "Category", "Elements"), Does.Contain(("IFCCOVERING", 1262L))
            .And.Contain(("IFCWALL", 652L))
            .And.Contain(("IFCWALLSTANDARDCASE", 282L))
            .And.Contain(("IFCWINDOW", 259L))
            .And.Contain(("IFCDOOR", 205L))
            .And.Contain(("IFCSPACE", 100L)));
        Assert.That(chart.Cell("Category", 0), Is.EqualTo("IFCCOVERING"), "sorted by count, largest first");
    }

    [Test]
    public void Schependomlaan_SpacesPerStorey()
    {
        var chart = PublicBuildings.Evaluate("schependomlaan-spaces-per-storey").Table("chart");
        Assert.That(Lookup(chart, "StoreyName", "Spaces"), Is.EqualTo(new[]
        {
            ("00 begane grond", 32L),
            ("01 eerste verdieping", 29L),
            ("02 tweede verdieping", 20L),
            ("03 derde verdieping", 19L),
        }));
    }

    [Test]
    public void Schependomlaan_RoomNames()
    {
        var chart = PublicBuildings.Evaluate("schependomlaan-room-names").Table("chart");
        Assert.That(Total(chart, "Spaces"), Is.EqualTo(100));
        Assert.That(Lookup(chart, "Name", "Spaces").Take(7), Is.EqualTo(new[]
        {
            ("entree", 11L),
            ("instal. ruimte", 11L),
            ("badkamer", 10L),
            ("keuken", 10L),
            ("mk", 10L),
            ("toilet", 10L),
            ("woonkamer", 10L),
        }));
    }

    [Test]
    public void DigitalHub_OpeningsPerStorey()
    {
        var chart = PublicBuildings.Evaluate("digitalhub-openings-per-storey").Table("chart");
        Assert.That(chart.Column("StoreyName"), Is.EqualTo(new[] { "B01_OKRD", "E00_OKRD", "E01_OKRD" }));
        Assert.That(chart.Column("IFCDOOR").Select(Count), Is.EqualTo(new long?[] { 13, 26, 25 }));
        Assert.That(chart.Column("IFCWINDOW").Select(Count), Is.EqualTo(new long?[] { 0, 21, 26 }),
            "the basement has no windows, and the pivot's count of no rows is 0");
        Assert.That(chart.Column("IFCSPACE").Select(Count), Is.EqualTo(new long?[] { 12, 26, 26 }));
    }

    [Test]
    public void DigitalHub_HeatingPerStorey()
    {
        var table = PublicBuildings.Evaluate("digitalhub-heating-per-storey").Table("heating");
        Assert.That(table.Column("Storey"), Is.EqualTo(new[] { "B01_OKRD", "E00_OKRD", "E01_OKRD" }));
        Assert.That(table.Column("PipeSegments").Select(Count), Is.EqualTo(new long?[] { 157, 373, 384 }));
        Assert.That(table.Column("PipeFittings").Select(Count), Is.EqualTo(new long?[] { 110, 310, 323 }));
        Assert.That(table.Column("SpaceHeaters").Select(Count), Is.EqualTo(new long?[] { 0, 29, 34 }));
        Assert.That(table.Column("Valves").Select(Count), Is.EqualTo(new long?[] { 25, 20, 20 }));
        Assert.That(table.Column("AllElements").Select(Count), Is.EqualTo(new long?[] { 302, 732, 761 }));
    }

    [Test]
    public void DigitalHub_FederatedModels()
    {
        var table = PublicBuildings.Evaluate("digitalhub-federated-models").Table("chart");
        Assert.That(Lookup(table, "Model", "Elements"), Is.EqualTo(new[]
        {
            ("DigitalHub_FM-ARC_v2", 777L),
            ("DigitalHub_FM-HZG_v2", 1795L),
            ("DigitalHub_FM-LFT_v2", 1310L),
            ("DigitalHub_FM-SAN_v2", 1010L),
        }));
    }

    /// <summary>site/data/buildings.json is what the graphs produce now; on a failure,
    /// regenerate it with PublicBuildingsSiteData.Write.</summary>
    [Test]
    public void SiteData_IsCurrent()
    {
        Assert.That(File.Exists(PublicBuildings.SiteDataFile), $"{PublicBuildings.SiteDataFile} is missing");
        Assert.That(
            File.ReadAllText(PublicBuildings.SiteDataFile).Replace("\r\n", "\n"),
            Is.EqualTo(PublicBuildings.SiteJson()),
            "site/data/buildings.json is stale: run the explicit test PublicBuildingsSiteData.Write");
    }

    /// <summary>The page ships bim-open-data's notice for the buildings unchanged.</summary>
    [Test]
    public void SiteNotice_IsCurrent()
    {
        Assert.That(File.Exists(PublicBuildings.SiteNoticeFile), $"{PublicBuildings.SiteNoticeFile} is missing");
        Assert.That(
            File.ReadAllText(PublicBuildings.SiteNoticeFile).Replace("\r\n", "\n"),
            Is.EqualTo(File.ReadAllText(PublicBuildings.NoticeFile).Replace("\r\n", "\n")),
            "site/data/NOTICE.md differs from bim-open-data's: run the explicit test PublicBuildingsSiteData.Write");
    }

    private static long? Count(object? cell)
        => cell is null or DBNull ? null : Convert.ToInt64(cell);

    private static long Total(IDataTable table, string column)
        => table.Column(column).Sum(c => Count(c) ?? 0);

    private static List<(string, long)> Lookup(IDataTable table, string label, string value)
        => Enumerable.Range(0, table.Rows.Count)
            .Select(r => ((string)table.Cell(label, r)!, Count(table.Cell(value, r)) ?? 0))
            .ToList();
}

/// <summary>Writes site/data/buildings.json from the graphs, and copies NOTICE.md beside it. Explicit: run it after a graph
/// or the pinned data changes, then commit the file.</summary>
[TestFixture, Explicit("Writes site/data/buildings.json")]
public sealed class PublicBuildingsSiteData
{
    [Test]
    public void Write()
    {
        if (PublicBuildings.MissingDataReason is { } reason)
            Assert.Ignore(reason);
        Directory.CreateDirectory(Path.GetDirectoryName(PublicBuildings.SiteDataFile)!);
        File.WriteAllText(PublicBuildings.SiteDataFile, PublicBuildings.SiteJson());
        File.Copy(PublicBuildings.NoticeFile, PublicBuildings.SiteNoticeFile, overwrite: true);
    }
}
