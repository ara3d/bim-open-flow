using System.Text.Json;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Store;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class EditorSessionTests
{
    [Test]
    public async Task GetBeforeAnyPut_ReturnsEmptySelection()
    {
        using var doc = await GetJson("/api/session");
        Assert.That(doc.RootElement.TryGetProperty("analysisId", out _), Is.False);
        Assert.That(doc.RootElement.GetProperty("selection").EnumerateArray(), Is.Empty);
    }

    [Test]
    public async Task PutThenGet_RoundTripsWithUpdatedUtcSet()
    {
        var response = await ApiTestServer.Client.PutAsync("/api/session",
            JsonContent("""{"analysisId":"session-round-trip","selection":["answer"]}"""));
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        using var put = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(put.RootElement.GetProperty("analysisId").GetString(), Is.EqualTo("session-round-trip"));
        Assert.That(put.RootElement.GetProperty("selection").EnumerateArray().Select(e => e.GetString()),
            Is.EqualTo(new[] { "answer" }));
        Assert.That(put.RootElement.GetProperty("updatedUtc").GetString(), Is.Not.Empty);

        using var got = await GetJson("/api/session");
        Assert.That(got.RootElement.GetProperty("analysisId").GetString(), Is.EqualTo("session-round-trip"));
        Assert.That(got.RootElement.GetProperty("updatedUtc").GetString(),
            Is.EqualTo(put.RootElement.GetProperty("updatedUtc").GetString()));
    }

    [Test]
    public async Task PutBadAnalysisId_Returns400ApiError()
    {
        var response = await ApiTestServer.Client.PutAsync("/api/session",
            JsonContent("""{"analysisId":"Bad Id","selection":[]}"""));
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task SecondEditorSessionsOnSameStore_ReadsHostsWrite()
    {
        var response = await ApiTestServer.Client.PutAsync("/api/session",
            JsonContent("""{"analysisId":"session-shared","selection":["a","b"]}"""));
        Assert.That((int)response.StatusCode, Is.EqualTo(200));

        var otherStore = new AnalysisStore(Path.Combine(ApiTestServer.RootDir, "analyses"));
        var otherEditor = new EditorSessions(otherStore);
        var read = otherEditor.Read();
        Assert.That(read.AnalysisId, Is.EqualTo("session-shared"));
        Assert.That(read.Selection, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void AnalysisStoreList_IgnoresTheSessionFile()
    {
        var store = new AnalysisStore(Path.Combine(ApiTestServer.RootDir, "analyses"));
        var editor = new EditorSessions(store);
        editor.Write(new(null, [], null), DateTimeOffset.UtcNow);
        Assert.That(store.List().Any(a => a.Id == EditorSessions.FileName), Is.False);
    }

    private static StringContent JsonContent(string json)
        => new(json, System.Text.Encoding.UTF8, "application/json");
}
