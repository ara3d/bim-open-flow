using System.Text.Json;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class EvalAndResultTests
{
    [Test]
    public async Task State_ReportsOkNodesWithStatusNames()
    {
        await PutAnalysis("state-case", ConstNegate());
        var text = await GetOk("/api/analyses/state-case/state");
        Assert.That(text, Does.Contain("\"analysisId\""));
        using var doc = JsonDocument.Parse(text);
        Assert.That(doc.RootElement.GetProperty("analysisId").GetString(), Is.EqualTo("state-case"));
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.That(nodes.Select(n => n.GetProperty("nodeId").GetString()), Is.EqualTo(new[] { "c", "n" }));
        Assert.That(nodes.Select(n => n.GetProperty("status").GetString()), Has.All.EqualTo("Ok"));
    }

    [Test]
    public async Task ScalarResult_BecomesOneCellSlice()
    {
        await PutAnalysis("result-case", ConstNegate());
        using var doc = await GetJson("/api/analyses/result-case/results/n/out");
        var root = doc.RootElement;
        var column = root.GetProperty("columns").EnumerateArray().Single();
        Assert.That(column.GetProperty("name").GetString(), Is.EqualTo("out"));
        Assert.That(column.GetProperty("type").GetString(), Is.EqualTo("Integer"));
        Assert.That(root.GetProperty("totalRows").GetInt32(), Is.EqualTo(1));
        Assert.That(root.GetProperty("skip").GetInt32(), Is.EqualTo(0));
        var row = root.GetProperty("rows").EnumerateArray().Single();
        Assert.That(row.EnumerateArray().Single().GetInt64(), Is.EqualTo(-42));
    }

    [Test]
    public async Task ResultPaging_SkipPastEndYieldsEmptyRows()
    {
        await PutAnalysis("paging-case", ConstNegate());
        using var doc = await GetJson("/api/analyses/paging-case/results/n/out?skip=1&take=10");
        Assert.That(doc.RootElement.GetProperty("rows").GetArrayLength(), Is.EqualTo(0));
        Assert.That(doc.RootElement.GetProperty("totalRows").GetInt32(), Is.EqualTo(1));
        Assert.That(doc.RootElement.GetProperty("skip").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task UnknownNodeOrPort_Returns404()
    {
        await PutAnalysis("missing-result", ConstNegate());
        var noNode = await ApiTestServer.Client.GetAsync("/api/analyses/missing-result/results/zz/out");
        Assert.That((int)noNode.StatusCode, Is.EqualTo(404));
        var noPort = await ApiTestServer.Client.GetAsync("/api/analyses/missing-result/results/n/nope");
        Assert.That((int)noPort.StatusCode, Is.EqualTo(404));
    }
}
