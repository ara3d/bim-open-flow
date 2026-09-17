using System.Text.Json;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class RunAndSseTests
{
    [Test]
    public async Task CreateListGetRun_RoundTrips()
    {
        await PutAnalysis("run-case", ConstNegate());

        var created = await ApiTestServer.Client.PostAsync("/api/analyses/run-case/runs", null);
        Assert.That((int)created.StatusCode, Is.EqualTo(200));
        using var summary = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var fileName = summary.RootElement.GetProperty("fileName").GetString()!;
        var graphHash = summary.RootElement.GetProperty("graphHash").GetString()!;
        Assert.That(fileName, Does.EndWith(".run.json"));
        Assert.That(summary.RootElement.GetProperty("timestampUtc").GetString(), Does.EndWith("Z"));

        using var list = await GetJson("/api/analyses/run-case/runs");
        var listed = list.RootElement.EnumerateArray()
            .Single(r => r.GetProperty("fileName").GetString() == fileName);
        Assert.That(listed.GetProperty("graphHash").GetString(), Is.EqualTo(graphHash));

        var runText = await GetOk($"/api/analyses/run-case/runs/{fileName}");
        using var run = JsonDocument.Parse(runText);
        Assert.That(run.RootElement.GetProperty("graphHash").GetString(), Is.EqualTo(graphHash));
    }

    [Test]
    public async Task CreateRun_MissingAnalysis_Returns404()
    {
        var response = await ApiTestServer.Client.PostAsync("/api/analyses/no-runs-here/runs", null);
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
    }

    [Test]
    public async Task GetRun_PathTraversalName_Returns400()
    {
        await PutAnalysis("run-guard", ConstNegate());
        var response = await ApiTestServer.Client.GetAsync("/api/analyses/run-guard/runs/..%2Fcurrent.dfg.json");
        Assert.That((int)response.StatusCode, Is.EqualTo(400).Or.EqualTo(404));
    }

    [Test]
    public async Task Sse_SendsInitialStateImmediately()
    {
        await PutAnalysis("sse-case", ConstNegate());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var response = await ApiTestServer.Client.GetAsync(
            "/api/analyses/sse-case/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/event-stream"));

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);
        string? data = null;
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (!line.StartsWith("data: ", StringComparison.Ordinal))
                continue;
            data = line["data: ".Length..];
            break;
        }
        Assert.That(data, Is.Not.Null);
        using var update = JsonDocument.Parse(data!);
        Assert.That(update.RootElement.GetProperty("analysisId").GetString(), Is.EqualTo("sse-case"));
        Assert.That(update.RootElement.GetProperty("nodes").GetArrayLength(), Is.EqualTo(2));
    }
}
