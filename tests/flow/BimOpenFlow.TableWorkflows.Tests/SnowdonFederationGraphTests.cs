using Ara3D.DataFlowEngine.TestKit;
using Ara3D.DataTable;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// samples/snowdon-analyses/federation-match.json over FederationExample's five-document
/// union: the storey-by-elevation rule (C5) and the confirmations join. The 12 expected
/// storey rows and their conflicts/warnings are the worked example in
/// docs/plans/snowdon-federation-build.md ("Match graph", "Worked example").
/// </summary>
[TestFixture]
public sealed class SnowdonFederationGraphTests
{
    private const string ConfirmStructL1Low =
        "storey,Struct,2,storey/0mm,Confirmed,tester,2026-01-01T00:00:00Z,confirmed L1_Low\n";

    private const string NonexistentLocalId =
        "storey,Struct,999,storey/0mm,Rejected,tester,2026-01-01T00:00:00Z,phantom local id\n";

    private const string Header =
        "concept,source_document,source_local_id,canonical_key,decision,decided_by,decided_at,note\n";

    private string _dir = null!;

    [SetUp]
    public void NewTempDir()
        => _dir = Path.Combine(Path.GetTempPath(), "bimopenflow-snowdon-federation", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DeleteTempDir()
    {
        try { Directory.Delete(_dir, recursive: true); }
        catch (IOException) { }
    }

    /// <summary>conflicts/warnings are comma-joined VARCHAR, not VARCHAR[] (see the graph's
    /// session.description): every sink and multi-input node round-trips its input table
    /// through Ara3D.BimOpenSchema.IO.DuckDbUtils.WriteTable, which has no branch for
    /// array-typed values.</summary>
    private static string[] AsStringArray(object? value)
        => value is string s && s.Length > 0 ? s.Split(',') : [];

    [Test]
    public void Graph_ValidatesAgainstTablePacks()
    {
        var built = SnowdonFederationFixture.Build(_dir, Header);
        Assert.That(built.Graph.Nodes, Is.Not.Empty);
        Assert.That(built.Graph.Validate(HostComposition.TablePacks()), Is.Empty);
    }

    /// <summary>One row per (canonical_key, source_document), in the graph's own
    /// ORDER BY: canonical_key, source_document, source_local_id. Deltas and booleans
    /// are compared with a tolerance that absorbs BOS's float32 Numbers column.</summary>
    private sealed record Expected(
        string CanonicalKey, string Document, string Name, string Status,
        string[] Conflicts, string[] Warnings, double? DeltaMm);

    private static readonly Expected[] ExpectedStoreyRows =
    [
        new("storey/-241402mm", "Site", "Datum", "Unmatched", [], ["global-id-in-other-cluster"], 0),
        new("storey/-5156mm", "Arch", "Parking", "Candidate", [], [], 0),
        new("storey/-5156mm", "Struct", "Parking", "Candidate", [], [], 0),
        new("storey/0mm", "Arch", "L1", "Candidate", [], [], 0),
        new("storey/0mm", "Elec", "L1", "Candidate", ["elevation-offset"], [], -12.71),
        new("storey/0mm", "Struct", "L1_Low", "Confirmed", ["name-differs"], [], 0),
        new("storey/14454mm", "Site", "Parapet", "Unmatched", ["name-in-other-cluster"], [], 0),
        new("storey/14530mm", "Arch", "Parapet", "Unmatched", ["name-in-other-cluster"], [], 0),
        new("storey/2464mm", "Arch", "L2", "Candidate", [], [], 0),
        new("storey/2464mm", "Elec", "L2", "Candidate", [], ["global-id-in-other-cluster"], 0),
        new("storey/2464mm", "Struct", "L2", "Candidate", [], [], 0),
        new("storey/unknown/Plumb/1", "Plumb", "P-L1", "Unmatched", ["length-unit-unknown"], [], null),
    ];

    [Test]
    public void TwelveStoreyRows_MatchTheWorkedExample_ColumnByColumn()
    {
        var built = SnowdonFederationFixture.Build(_dir, Header + ConfirmStructL1Low);
        var session = TableReads.NewTableSession();
        session.Evaluate(built.Graph);
        var table = session.Table("correspondence");

        var ruleRows = Enumerable.Range(0, table.Rows.Count)
            .Where(r => (string)table.Cell("rule_status", r)! != "NoRuleRow")
            .ToList();
        Assert.That(ruleRows, Has.Count.EqualTo(12));

        for (var i = 0; i < ExpectedStoreyRows.Length; i++)
        {
            var row = ruleRows[i];
            var expected = ExpectedStoreyRows[i];
            Assert.Multiple(() =>
            {
                Assert.That(table.Cell("canonical_key", row), Is.EqualTo(expected.CanonicalKey), $"row {i} canonical_key");
                Assert.That(table.Cell("source_document", row), Is.EqualTo(expected.Document), $"row {i} document");
                Assert.That(table.Cell("source_name", row), Is.EqualTo(expected.Name), $"row {i} name");
                Assert.That(table.Cell("status", row), Is.EqualTo(expected.Status), $"row {i} status");
                Assert.That(AsStringArray(table.Cell("conflicts", row)), Is.EqualTo(expected.Conflicts), $"row {i} conflicts");
                Assert.That(AsStringArray(table.Cell("warnings", row)), Is.EqualTo(expected.Warnings), $"row {i} warnings");
                if (expected.DeltaMm is { } delta)
                    Assert.That(Convert.ToDouble(table.Cell("elevation_delta_mm", row)), Is.EqualTo(delta).Within(0.05), $"row {i} delta");
                else
                    Assert.That(table.Cell("elevation_delta_mm", row), Is.Null, $"row {i} delta");
            });
        }
    }

    [Test]
    public void TestConfirmations_GiveConfirmedAndNoRuleRow()
    {
        var built = SnowdonFederationFixture.Build(_dir, Header + ConfirmStructL1Low + NonexistentLocalId);
        var session = TableReads.NewTableSession();
        session.Evaluate(built.Graph);
        var table = session.Table("correspondence");

        var l1Low = Enumerable.Range(0, table.Rows.Count)
            .Single(r => (string)table.Cell("source_document", r)! == "Struct"
                         && Convert.ToInt64(table.Cell("source_local_id", r)) == 2);
        Assert.That(table.Cell("status", l1Low), Is.EqualTo("Confirmed"));

        var noRuleRows = Enumerable.Range(0, table.Rows.Count)
            .Where(r => (string)table.Cell("rule_status", r)! == "NoRuleRow")
            .ToList();
        Assert.That(noRuleRows, Has.Count.EqualTo(1));
        Assert.That(table.Cell("status", noRuleRows[0]), Is.EqualTo("Rejected"));
        Assert.That(Convert.ToInt64(table.Cell("source_local_id", noRuleRows[0])), Is.EqualTo(999));
    }

    [Test]
    public void Run_WritesTheParquetFile()
    {
        var built = SnowdonFederationFixture.Build(_dir, Header + ConfirmStructL1Low);
        var session = TableReads.NewTableSession();
        session.Evaluate(built.Graph);
        session.Session.Run();

        Assert.That(File.Exists(built.Correspondence), Is.True);
        Assert.That(new FileInfo(built.Correspondence).Length, Is.GreaterThan(0));
    }

    [Test]
    public void SecondRun_LeavesTheConfirmationsCsvByteIdentical()
    {
        var built = SnowdonFederationFixture.Build(_dir, Header + ConfirmStructL1Low);
        var before = File.ReadAllBytes(built.Confirmations);

        var session = TableReads.NewTableSession();
        session.Evaluate(built.Graph);
        session.Session.Run();
        session.Session.Run();

        Assert.That(File.ReadAllBytes(built.Confirmations), Is.EqualTo(before));
    }
}
