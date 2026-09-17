using BimOpenFlow.Publishing;

namespace BimOpenFlow.Dashboards.Tests;

public class DashboardGeneratorTests
{
    private static readonly VizBundle StubBundle = new("var BofViz = /* stub bundle */ {};");

    private static DashboardSpec Spec(params DashboardItem[] items)
        => new("Test Dashboard", items);

    [Test]
    public void FromRun_EmbedsBundleDataAndMountScript()
    {
        var html = DashboardGenerator.FromRun(
            TestRuns.Sample(),
            Spec(
                new("t.out", DashboardWidget.Table, "My Table"),
                new("t.out", DashboardWidget.BarChart, OptionsJson: "{\"categoryColumn\":\"cat\",\"valueColumn\":\"val\"}")),
            StubBundle);

        Assert.That(html, Does.Contain(StubBundle.Js));
        Assert.That(html, Does.Contain("const bofDashboardData = {"));
        Assert.That(html, Does.Contain("\"name\":\"cat\",\"type\":\"Text\""));
        Assert.That(html, Does.Contain("BofViz.DataTableView.mount(document.getElementById(\"bof-mount-0\"), bofDashboardData[\"t.out\"]);"));
        Assert.That(html, Does.Contain("BofViz.BarChart.mount(document.getElementById(\"bof-mount-1\"), bofDashboardData[\"t.out\"], {\"categoryColumn\":\"cat\",\"valueColumn\":\"val\"});"));
        Assert.That(html, Does.Contain("<h2>My Table</h2>"));
        Assert.That(html, Does.Contain(TestRuns.GraphHash));
    }

    [Test]
    public void FromRun_IsDeterministic()
    {
        var spec = Spec(new DashboardItem("t.out", DashboardWidget.LineChart));
        var first = DashboardGenerator.FromRun(TestRuns.Sample(), spec, StubBundle);
        var second = DashboardGenerator.FromRun(TestRuns.Sample(), spec, StubBundle);
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void FromRun_UnknownPortThrows()
        => Assert.Throws<ArgumentException>(() => DashboardGenerator.FromRun(
            TestRuns.Sample(), Spec(new DashboardItem("missing.port", DashboardWidget.Table)), StubBundle));

    [Test]
    public void FromRun_NonTablePortThrows()
        => Assert.Throws<ArgumentException>(() => DashboardGenerator.FromRun(
            TestRuns.Sample(), Spec(new DashboardItem("s.out", DashboardWidget.Table)), StubBundle));

    [Test]
    public void DashboardItem_InvalidOptionsJsonThrows()
        => Assert.Throws<ArgumentException>(() =>
            new DashboardItem("t.out", DashboardWidget.Table, OptionsJson: "{not json"));

    [Test]
    public void FromRun_WithRealBundleWhenBuilt()
    {
        var path = VizBundle.FindInRepo(TestContext.CurrentContext.TestDirectory);
        if (path is null)
            Assert.Ignore("viz.iife.js not built; run 'npm run -w @bimopenflow/viz bundle'");
        var html = DashboardGenerator.FromRun(
            TestRuns.Sample(), Spec(new DashboardItem("t.out", DashboardWidget.Table)), VizBundle.FromFile(path!));
        Assert.That(html, Does.Contain("BofViz"));
    }
}
