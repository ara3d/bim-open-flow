using System.Text.Json;
using Ara3D.NodeGraph;
using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class ModelAndDocumentTests
{
    [Test]
    public async Task ListModels_ReturnsScannedFileWithCamelCaseFields()
    {
        var text = await GetOk("/api/models");
        Assert.That(text, Does.Contain("\"sizeBytes\""));
        Assert.That(text, Does.Contain("\"lastWriteUtc\""));
        using var doc = JsonDocument.Parse(text);
        var model = doc.RootElement.EnumerateArray()
            .Single(m => m.GetProperty("id").GetString() == "sample-model.bos");
        Assert.That(model.GetProperty("name").GetString(), Is.EqualTo("sample model"));
        Assert.That(model.GetProperty("kind").GetString(), Is.EqualTo("Bos"));
        Assert.That(model.GetProperty("sizeBytes").GetInt32(), Is.EqualTo(5));
        Assert.That(model.GetProperty("lastWriteUtc").GetString(), Does.EndWith("Z"));
    }

    [Test]
    public async Task PutThenGet_RoundTripsByteIdentical()
    {
        var doc = ConstNegate();
        var response = await PutText("/api/analyses/round-trip", doc.ToCanonicalJson());
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        using var summary = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(summary.RootElement.GetProperty("id").GetString(), Is.EqualTo("round-trip"));
        Assert.That(summary.RootElement.GetProperty("graphHash").GetString(),
            Is.EqualTo(doc.ComputeGraphHash()));

        var fetched = await GetOk("/api/analyses/round-trip");
        Assert.That(fetched, Is.EqualTo(doc.ToCanonicalJson()));

        using var list = await GetJson("/api/analyses");
        Assert.That(list.RootElement.EnumerateArray()
            .Any(a => a.GetProperty("id").GetString() == "round-trip"), Is.True);
    }

    [Test]
    public async Task SecondPut_ArchivesFirstVersionInHistory()
    {
        var first = ConstNegate("1");
        await PutAnalysis("history-case", first);
        await PutAnalysis("history-case", ConstNegate("2"));
        using var history = await GetJson("/api/analyses/history-case/history");
        var versions = history.RootElement.EnumerateArray().ToList();
        Assert.That(versions, Has.Count.EqualTo(1));
        Assert.That(versions[0].GetProperty("version").GetInt32(), Is.EqualTo(1));
        Assert.That(versions[0].GetProperty("graphHash").GetString(),
            Is.EqualTo(first.ComputeGraphHash()));
    }

    [Test]
    public async Task PutMalformedJson_Returns400ApiError()
    {
        var response = await PutText("/api/analyses/bad-json", "{not json");
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task PutUnknownNodeKind_Returns400ApiError()
    {
        const string body = """
            {"structure":{"edges":[],"nodes":[{"id":"x","kind":"no.such","version":1}]},"values":{}}
            """;
        var response = await PutText("/api/analyses/bad-kind", body);
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Does.Contain("no.such"));
    }

    [Test]
    public async Task GetMissingAnalysis_Returns404ApiError()
    {
        var response = await ApiTestServer.Client.GetAsync("/api/analyses/never-created");
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Does.Contain("never-created"));
    }
}
