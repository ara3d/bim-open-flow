using System.Text.Json;
using Ara3D.MCP;
using BimOpenFlow.Mcp;

namespace BimOpenFlow.Mcp.Tests;

public sealed class DocumentToolTests : FlowToolFixture
{
    [Test]
    public void ListModels_SeesTheSampleBos()
    {
        var models = Json(FlowDocumentTools.ListModels(Services)).EnumerateArray().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(models, Has.Count.EqualTo(1));
            Assert.That(models[0].GetProperty("id").GetString(), Is.EqualTo("sample.bos"));
            Assert.That(models[0].GetProperty("kind").GetString(), Is.EqualTo("Bos"));
        });
    }

    [Test]
    public void GetAnalysis_RoundTripsThroughSaveAnalysis()
    {
        AuthorCameraSort("original");
        var original = Json(FlowDocumentTools.GetAnalysis(Services, "original"));

        FlowDocumentTools.SaveAnalysis(Services, "copy", original.GetProperty("json").GetString()!);
        var copy = Json(FlowDocumentTools.GetAnalysis(Services, "copy"));

        Assert.That(copy.GetProperty("graphHash").GetString(),
            Is.EqualTo(original.GetProperty("graphHash").GetString()));
    }

    [Test]
    public void SaveAnalysis_InvalidDocumentDoesNotLand()
    {
        var badDoc = """
            {"structure": {"nodes": [{"id": "x", "kind": "no.such", "version": 1}], "edges": []}, "values": {}}
            """;
        Assert.That(
            Assert.Throws<ArgumentException>(() =>
                FlowDocumentTools.SaveAnalysis(Services, "bad", badDoc))!.Message,
            Does.Contain("Invalid graph"));
        Assert.That(Services.Host.Store.Exists("bad"), Is.False);
    }

    [Test]
    public void ListAnalyses_ReportsSavedGraphs()
    {
        AuthorCameraSort("one");
        var analyses = Json(FlowDocumentTools.ListAnalyses(Services)).EnumerateArray().ToList();
        Assert.That(analyses.Single().GetProperty("id").GetString(), Is.EqualTo("one"));
    }

    [Test]
    public void GetNodeCatalog_CoversAllFourPacks()
    {
        var kinds = Json(FlowDocumentTools.GetNodeCatalog(Services)).EnumerateArray()
            .Select(n => n.GetProperty("kind").GetString())
            .ToList();
        Assert.That(kinds,
            Is.SupersetOf(new[] { "bos.load", "view3d.instances", "check.rule", "sink.exportCsv" }));
    }

    [Test]
    public async Task ToolRunner_ShapesFailuresAsErrorEnvelopes()
    {
        var body = await ToolRunner.RunAsync(
            () => FlowEditTools.AddNode(Services, "a", "x", "no.such", version: null));
        using var envelope = JsonDocument.Parse(body);
        Assert.Multiple(() =>
        {
            Assert.That(envelope.RootElement.GetProperty("ok").GetBoolean(), Is.False);
            Assert.That(envelope.RootElement.GetProperty("error").GetString(),
                Does.Contain("Unknown node kind"));
            Assert.That(envelope.RootElement.GetProperty("type").GetString(),
                Is.EqualTo("ArgumentException"));
        });
    }
}
