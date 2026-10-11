using System.Text.Json;
using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>
/// Graph inputs over the host: the document declares them, the store keeps the current values
/// beside it, sessions evaluate with them, the HTTP API lists and changes them, and a run record
/// pins them. The graph is const(`$n`) -> negate, so the input n is the number that comes out negated.
/// </summary>
[TestFixture]
public sealed class GraphInputTests
{
    private static GraphDocument WithInput(string id = "n")
        => Graph
            .Node("c", "test.const", ("kind", "Integer"), ("value", "$" + id))
            .Node("n", "test.negate")
            .Connect("c.out", "n.in")
            .Build()
            .SetInput(new GraphInput(id, ParamKind.Integer, "42", "The number to negate"));

    private static long Negated(JsonDocument slice)
        => slice.RootElement.GetProperty("rows")[0][0].GetInt64();

    private static async Task<string> ErrorText(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("error").GetString()!;
    }

    private static Task<HttpResponseMessage> PutValue(string id, string name, string value)
        => PutText($"/api/analyses/{id}/inputs/{name}", value);

    [Test]
    public async Task Http_ListsInputsWithDefaultsAndCurrentValues_AndChangesOne()
    {
        await PutAnalysis("inputs-http", WithInput());

        using (var before = await GetJson("/api/analyses/inputs-http/inputs"))
        {
            var input = before.RootElement.EnumerateArray().Single();
            Assert.Multiple(() =>
            {
                Assert.That(input.GetProperty("name").GetString(), Is.EqualTo("n"));
                Assert.That(input.GetProperty("kind").GetString(), Is.EqualTo("Integer"));
                Assert.That(input.GetProperty("default").GetString(), Is.EqualTo("42"));
                Assert.That(input.GetProperty("value").GetString(), Is.EqualTo("42"));
                Assert.That(input.GetProperty("description").GetString(), Is.EqualTo("The number to negate"));
            });
        }
        using (var slice = await GetJson("/api/analyses/inputs-http/results/n/out"))
            Assert.That(Negated(slice), Is.EqualTo(-42));

        var put = await PutValue("inputs-http", "n", "7");
        Assert.That((int)put.StatusCode, Is.EqualTo(200));
        using (var after = JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
        {
            var input = after.RootElement.EnumerateArray().Single();
            Assert.That(input.GetProperty("value").GetString(), Is.EqualTo("7"));
            Assert.That(input.GetProperty("default").GetString(), Is.EqualTo("42"), "the default is the document's");
        }
        using (var slice = await GetJson("/api/analyses/inputs-http/results/n/out"))
            Assert.That(Negated(slice), Is.EqualTo(-7), "every reader re-evaluates");
        using (var state = await GetJson("/api/analyses/inputs-http/state"))
            Assert.That(state.RootElement.GetProperty("graphInputs")[0].GetProperty("value").GetString(), Is.EqualTo("7"));
    }

    [Test]
    public async Task Http_ChangingAValueDoesNotChangeTheGraphHash()
    {
        await PutAnalysis("inputs-hash", WithInput());
        using var before = await GetJson("/api/analyses/inputs-hash/state");
        await PutValue("inputs-hash", "n", "3");
        using var after = await GetJson("/api/analyses/inputs-hash/state");
        Assert.That(after.RootElement.GetProperty("graphHash").GetString(),
            Is.EqualTo(before.RootElement.GetProperty("graphHash").GetString()));
    }

    [Test]
    public async Task Http_RejectsAnUnknownNameAndABadValue_AndKeepsTheOldValue()
    {
        await PutAnalysis("inputs-bad", WithInput());
        var unknown = await PutValue("inputs-bad", "other", "1");
        Assert.That((int)unknown.StatusCode, Is.EqualTo(400));
        Assert.That(await ErrorText(unknown), Does.Contain("'other'"));

        var bad = await PutValue("inputs-bad", "n", "seven");
        Assert.That((int)bad.StatusCode, Is.EqualTo(400));
        Assert.That(await ErrorText(bad), Does.Contain("'n'"));

        using var slice = await GetJson("/api/analyses/inputs-bad/results/n/out");
        Assert.That(Negated(slice), Is.EqualTo(-42));
    }

    [Test]
    public async Task Http_RunRecordPinsTheInputAndItsValue()
    {
        await PutAnalysis("inputs-run", WithInput());
        await PutValue("inputs-run", "n", "9");
        var created = await ApiTestServer.Client.PostAsync("/api/analyses/inputs-run/runs", null);
        Assert.That((int)created.StatusCode, Is.EqualTo(200));
        using var summary = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var run = await GetOk($"/api/analyses/inputs-run/runs/{summary.RootElement.GetProperty("fileName").GetString()}");
        using var record = JsonDocument.Parse(run);
        Assert.That(record.RootElement.GetProperty("graphInputs").GetProperty("n").GetString(), Is.EqualTo("9"));
    }

    [Test]
    public async Task Http_AnalysisWithoutInputsReportsNone()
    {
        await PutAnalysis("inputs-none", ConstNegate());
        using var inputs = await GetJson("/api/analyses/inputs-none/inputs");
        Assert.That(inputs.RootElement.GetArrayLength(), Is.EqualTo(0));
        using var state = await GetJson("/api/analyses/inputs-none/state");
        Assert.That(state.RootElement.TryGetProperty("graphInputs", out _), Is.False);
    }

    private string _root = null!;

    [SetUp]
    public void NewRoot()
        => _root = Path.Combine(Path.GetTempPath(), "bof-graph-input-tests", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DeleteRoot()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
    }

    [Test]
    public void Sessions_KeepValuesInTheStore_AndAnotherSessionsObjectSeesThem()
    {
        var store = new AnalysisStore(_root);
        store.Save("a", WithInput());
        var first = new AnalysisSessions(store, TestNodes.Registry);
        first.SetGraphInputs("a", new Dictionary<string, string> { ["n"] = "5" });
        Assert.That(store.LoadGraphInputs("a"), Is.EqualTo(new Dictionary<string, string> { ["n"] = "5" }));

        var second = new AnalysisSessions(store, TestNodes.Registry);
        Assert.That(second.Snapshot("a").GraphInputs!["n"], Is.EqualTo("5"));

        first.SetGraphInputs("a", new Dictionary<string, string> { ["n"] = "6" });
        Assert.That(second.Snapshot("a").GraphInputs!["n"], Is.EqualTo("6"), "a changed values file is noticed");
    }

    [Test]
    public void Sessions_IgnoreStoredValuesTheDocumentNoLongerAccepts()
    {
        var store = new AnalysisStore(_root);
        store.Save("a", WithInput());
        var sessions = new AnalysisSessions(store, TestNodes.Registry);
        sessions.SetGraphInputs("a", new Dictionary<string, string> { ["n"] = "5" });

        store.Save("a", WithInput().SetInput(new GraphInput("n", ParamKind.Boolean, "true")));
        store.SaveGraphInputs("a", new Dictionary<string, string> { ["n"] = "5", ["gone"] = "x" });
        var reopened = new AnalysisSessions(store, TestNodes.Registry).Snapshot("a");
        Assert.That(reopened.GraphInputs!["n"], Is.EqualTo("true"), "5 is not a Boolean, so the default applies");
    }

    [Test]
    public void Sessions_RejectAnInvalidValueWithoutStoringIt()
    {
        var store = new AnalysisStore(_root);
        store.Save("a", WithInput());
        var sessions = new AnalysisSessions(store, TestNodes.Registry);
        Assert.Throws<InvalidGraphException>(
            () => sessions.SetGraphInputs("a", new Dictionary<string, string> { ["n"] = "x" }));
        Assert.That(store.LoadGraphInputs("a"), Is.Empty);
    }

    [Test]
    public void Store_ValuesFileRoundTripsAndEmptyRemovesIt()
    {
        var store = new AnalysisStore(_root);
        store.Save("a", WithInput());
        var stamp = store.Stamp("a");
        store.SaveGraphInputs("a", new Dictionary<string, string> { ["z"] = "C:\\x\\y.duckdb", ["a"] = "1" });
        Assert.That(store.LoadGraphInputs("a"), Is.EqualTo(new Dictionary<string, string> { ["z"] = "C:\\x\\y.duckdb", ["a"] = "1" }));
        Assert.That(store.Stamp("a"), Is.Not.EqualTo(stamp));
        store.SaveGraphInputs("a", new Dictionary<string, string>());
        Assert.That(store.LoadGraphInputs("a"), Is.Empty);
        Assert.That(store.Stamp("a"), Is.EqualTo(stamp));
    }
}
