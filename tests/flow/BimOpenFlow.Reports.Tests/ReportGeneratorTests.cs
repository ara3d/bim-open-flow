using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Reports.Tests;

public class ReportGeneratorTests
{
    private static readonly ReportOptions Options = new("Compliance Report");

    [Test]
    public void FromRun_ContainsNoScript()
        => Assert.That(ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options),
            Does.Not.Contain("<script").IgnoreCase);

    [Test]
    public void FromRun_VerdictSectionAppearsForVerdictTables()
    {
        var html = ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options);
        Assert.That(html, Does.Contain("Verdict summary"));
        Assert.That(html, Does.Contain("checks.out"));
        Assert.That(html, Does.Contain("bof-verdict-fail"));
        Assert.That(html, Does.Contain(">Fail</td>"));
    }

    [Test]
    public void FromRun_NoVerdictSectionOtherwise()
    {
        var html = ReportGenerator.FromRun(TestRuns.WithoutVerdicts(), Options);
        Assert.That(html, Does.Not.Contain("Verdict summary"));
        Assert.That(html, Does.Contain("Evidence"));
        Assert.That(html, Does.Contain("Number: <code>7.5</code>"));
    }

    [Test]
    public void FromRun_UnknownVerdictTextIsNotAVerdictTable()
        => Assert.That(ReportGenerator.FromRun(TestRuns.WithBadVerdictText(), Options),
            Does.Not.Contain("Verdict summary"));

    [Test]
    public void FromRun_ContainsProvenanceHashes()
    {
        var html = ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options);
        Assert.That(html, Does.Contain(TestRuns.GraphHash));
        Assert.That(html, Does.Contain(TestRuns.InputHash));
        Assert.That(html, Does.Contain(TestRuns.OutputHash));
        Assert.That(html, Does.Contain("model.ifc"));
        Assert.That(html, Does.Contain("2026-08-31T00:00:00.000Z"));
    }

    [Test]
    public void FromRun_CapsEvidenceRows()
    {
        var html = ReportGenerator.FromRun(TestRuns.WithVerdicts(), new("R", MaxEvidenceRows: 2));
        Assert.That(html, Does.Contain("Showing 2 of 4 rows"));
        Assert.That(html, Does.Contain("Showing 2 of 3 rows"));
    }

    [Test]
    public void FromRun_IsDeterministic()
    {
        var first = ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options);
        var second = ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options);
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void FromRun_HasPrintRules()
        => Assert.That(ReportGenerator.FromRun(TestRuns.WithVerdicts(), Options),
            Does.Contain("@media print"));

    [Test]
    public void VerdictCounts_WorstFollowsSeverityOrder()
    {
        Assert.That(new VerdictCounts(1, 1, 1, 1).Worst, Is.EqualTo(Verdict.Fail));
        Assert.That(new VerdictCounts(1, 0, 1, 1).Worst, Is.EqualTo(Verdict.NeedsReview));
        Assert.That(new VerdictCounts(1, 0, 0, 1).Worst, Is.EqualTo(Verdict.InfoNotAvailable));
        Assert.That(new VerdictCounts(1, 0, 0, 0).Worst, Is.EqualTo(Verdict.Pass));
    }

    [Test]
    public void VerdictTables_CountsSampleTable()
    {
        var table = ((TableValue)TestRuns.WithVerdicts().RecordedOutputs["checks.out"]).Table;
        Assert.That(VerdictTables.IsVerdictTable(table), Is.True);
        Assert.That(VerdictTables.Count(table),
            Is.EqualTo(new VerdictCounts(Pass: 2, Fail: 1, NeedsReview: 1, InfoNotAvailable: 0)));
    }

    [Test]
    public void VerdictTables_RejectsPlainTable()
    {
        var table = ((TableValue)TestRuns.WithoutVerdicts().RecordedOutputs["data.out"]).Table;
        Assert.That(VerdictTables.IsVerdictTable(table), Is.False);
        Assert.Throws<ArgumentException>(() => VerdictTables.Count(table));
    }
}
