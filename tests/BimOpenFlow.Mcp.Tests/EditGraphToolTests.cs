using System.Text.Json;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Mcp.Tests;

/// <summary>editGraph: many edits, one validation, one save; a bad edit saves nothing.</summary>
public sealed class EditGraphToolTests : FlowToolFixture
{
    private const string Build = """
        [
          {"op":"addNode","nodeId":"cam","kind":"view3d.camera"},
          {"op":"setParam","nodeId":"cam","name":"name","value":"front"},
          {"op":"addNode","nodeId":"sort","kind":"table.sort"},
          {"op":"setParam","nodeId":"sort","name":"by","value":"name"},
          {"op":"connect","from":"cam.camera","to":"sort.table"}
        ]
        """;

    [Test]
    public void AppliesEveryEditAndSavesOnce()
    {
        var result = Json(FlowEditTools.EditGraph(Services, "batch", Build));
        Assert.That(result.GetProperty("applied").GetInt32(), Is.EqualTo(5));
        var doc = Services.Host.Store.Load("batch");
        Assert.That(doc.Nodes.Select(n => n.Id), Is.EqualTo(new[] { "cam", "sort" }));
        Assert.That(doc.Edges, Has.Count.EqualTo(1));
        Assert.That(doc.Values["cam"]["name"], Is.EqualTo("front"));
        Assert.That(result.GetProperty("graphHash").GetString(), Is.EqualTo(doc.ComputeGraphHash()));
        Assert.That(Services.Host.Store.History("batch"), Is.Empty, "one save, no archived versions");
    }

    [Test]
    public void ContinuesFromTheSavedGraph()
    {
        FlowEditTools.EditGraph(Services, "batch", Build);
        FlowEditTools.EditGraph(Services, "batch", """[{"op":"removeNode","nodeId":"sort"}]""");
        Assert.That(Services.Host.Store.Load("batch").Nodes.Select(n => n.Id), Is.EqualTo(new[] { "cam" }));
    }

    [Test]
    public void ABadEditNamesItselfAndSavesNothing()
    {
        var error = Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, "batch", """
            [
              {"op":"addNode","nodeId":"cam","kind":"view3d.camera"},
              {"op":"addNode","nodeId":"x","kind":"no.such"}
            ]
            """));
        Assert.That(error!.Message, Does.StartWith("Edit 2 (addNode x)").And.Contain("Unknown node kind"));
        Assert.That(Services.Host.Store.Exists("batch"), Is.False);
    }

    [Test]
    public void MalformedInputIsAnArgumentError()
    {
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, "batch", "not json"))!.Message,
            Does.Contain("not valid JSON"));
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, "batch", "[]"))!.Message,
            Does.Contain("non-empty"));
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, "batch", """[{"op":"connect","from":"a.b"}]"""))!.Message,
            Does.Contain("'to' is required"));
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, "batch", """[{"op":"rename"}]"""))!.Message,
            Does.Contain("Unknown op"));
    }

    [Test]
    public void LineBreaksInsideValuesAreAccepted()
    {
        // Models write multi-line SQL as real line breaks inside the JSON string.
        FlowEditTools.EditGraph(Services, "batch", "[{\"op\":\"addNode\",\"nodeId\":\"cam\",\"kind\":\"view3d.camera\"},\n"
            + "{\"op\":\"setParam\",\"nodeId\":\"cam\",\"name\":\"name\",\"value\":\"line one\nline two\ttabbed\"}]");
        Assert.That(Services.Host.Store.Load("batch").Values["cam"]["name"], Is.EqualTo("line one\nline two\ttabbed"));
        Assert.That(FlowEditTools.EscapeControlCharactersInStrings("[\"a\\\"b\nc\"]"), Is.EqualTo("[\"a\\\"b\\nc\"]"),
            "an escaped quote does not end the string");
    }

    [Test]
    public void IsRegisteredAsATool()
    {
        using var mcp = new Ara3D.MCP.McpServer(Ara3D.MCP.McpServer.DefaultPort, "test", "0", transport: Ara3D.MCP.McpTransport.Http);
        FlowMcpServer.RegisterTools(mcp, Services);
        var listed = mcp.HandlePost("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");
        using var doc = JsonDocument.Parse(listed.JsonBody);
        var names = doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.That(names, Does.Contain("editGraph"));
    }
}
