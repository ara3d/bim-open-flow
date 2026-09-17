using BimOpenFlow.Nodes.Effects;
using static BimOpenFlow.Nodes.Effects.Tests.TestSupport;

namespace BimOpenFlow.Nodes.Effects.Tests;

public sealed class ReportTests
{
    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = NewTempDir();

    [TearDown]
    public void TearDown()
        => DeleteTempDir(_dir);

    [Test]
    public void HtmlContainsTitleHeadersAndCells()
    {
        var path = Path.Combine(_dir, "report.html");
        var outputs = new ReportNode().Eval(
            FakeContext.Run, TableInput(FixtureTable()),
            Params(("path", path), ("title", "My <Report> & Co")));

        var html = File.ReadAllText(path);
        Assert.That(html, Does.Contain("<title>My &lt;Report&gt; &amp; Co</title>"));
        Assert.That(html, Does.Contain("<h1>My &lt;Report&gt; &amp; Co</h1>"));
        Assert.That(html, Does.Contain("<th>name</th>"));
        Assert.That(html, Does.Contain("<th>flag</th>"));
        Assert.That(html, Does.Contain("<td>plain</td>"));
        Assert.That(html, Does.Contain("<td>with, comma</td>"));
        Assert.That(html, Does.Contain("<td>0.5</td>"));
        Assert.That(html, Does.Contain("<td>true</td>"));
        Assert.That(html, Does.Contain("<td></td>"));

        var summary = OutputTable(outputs);
        Assert.That(Cell(summary, "path"), Is.EqualTo(path));
        Assert.That(Cell(summary, "rowCount"), Is.EqualTo(3L));
    }
}
