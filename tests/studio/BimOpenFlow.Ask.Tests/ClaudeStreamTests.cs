using BimOpenFlow.Ask;

namespace BimOpenFlow.Ask.Tests;

/// <summary>Exercises ClaudeStream against hand-written stream-json lines, so no real claude
/// process is needed.</summary>
public sealed class ClaudeStreamTests
{
    private static readonly string[] SixLineExample =
    [
        """{"type":"system","subtype":"init","session_id":"s1","mcp_servers":[{"name":"bimopenflow","status":"connected"}]}""",
        """{"type":"assistant","message":{"content":[{"type":"text","text":"Let me look."}]}}""",
        """{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"mcp__bimopenflow__evaluate","input":{"id":"ask-walls"}}]}}""",
        """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":[{"type":"text","text":"{\"ok\":true,\"data\":{\"nodes\":[{\"nodeId\":\"answer\",\"status\":\"Ok\"}]}}"}],"is_error":false}]}}""",
        """{"type":"assistant","message":{"content":[{"type":"text","text":"1277 walls."}]}}""",
        """{"type":"result","subtype":"success","is_error":false,"result":"1277 walls.","session_id":"s1","num_turns":3,"usage":{"input_tokens":10,"cache_read_input_tokens":900,"cache_creation_input_tokens":90,"output_tokens":40}}""",
    ];

    [Test]
    public void TheSixLineExampleGivesTheStatedEventsAndResult()
    {
        var stream = new ClaudeStream("bimopenflow");
        var events = SixLineExample.SelectMany(stream.Read).ToList();

        Assert.That(events, Has.Count.EqualTo(2));
        Assert.That(events[0].Type, Is.EqualTo("text"));
        Assert.That(events[0].Text, Is.EqualTo("Let me look."));
        Assert.That(events[1].Type, Is.EqualTo("tool"));
        Assert.That(events[1].Name, Is.EqualTo("evaluate"));
        Assert.That(events[1].Args!["id"]!.GetValue<string>(), Is.EqualTo("ask-walls"));
        Assert.That(events[1].Ok, Is.True);
        Assert.That(events[1].Summary, Is.EqualTo("1 nodes Ok"));

        Assert.That(stream.SessionId, Is.EqualTo("s1"));
        Assert.That(stream.Servers, Is.EqualTo(new[] { new McpServerStatus("bimopenflow", "connected") }));
        Assert.That(stream.Result, Is.EqualTo(new ClaudeResult(false, "success", "1277 walls.", 3, 1000, 40, "s1")));
    }

    [Test]
    public void ToolResultContentAsAPlainStringWorks()
    {
        var stream = new ClaudeStream("bimopenflow");
        stream.Read("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"mcp__bimopenflow__listAnalyses","input":{}}]}}""");
        var events = stream.Read(
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"{\"ok\":true,\"data\":{\"tables\":[{\"name\":\"a\",\"columns\":[\"x\"]}]}}","is_error":false}]}}""");

        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Ok, Is.True);
    }

    [Test]
    public void IsErrorGivesOkFalseWithTheErrorTextAsSummary()
    {
        var stream = new ClaudeStream("bimopenflow");
        stream.Read("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"mcp__bimopenflow__evaluate","input":{}}]}}""");
        var events = stream.Read(
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":[{"type":"text","text":"boom"}],"is_error":true}]}}""");

        Assert.That(events[0].Ok, Is.False);
        Assert.That(events[0].Summary, Is.EqualTo("boom"));
    }

    [Test]
    public void AToolWithoutOurPrefixKeepsItsName()
    {
        Assert.That(ClaudeStream.ToolName("bimopenflow", "someOtherTool"), Is.EqualTo("someOtherTool"));
    }

    [Test]
    public void NonJsonAndStreamEventLinesAreIgnored()
    {
        var stream = new ClaudeStream("bimopenflow");
        Assert.That(stream.Read("not json at all"), Is.Empty);
        Assert.That(stream.Read("""{"type":"stream_event","event":{}}"""), Is.Empty);
    }

    [Test]
    public void ErrorMaxTurnsGivesIsErrorTrue()
    {
        var stream = new ClaudeStream("bimopenflow");
        stream.Read("""{"type":"result","subtype":"error_max_turns","is_error":true,"result":"","session_id":"s1","num_turns":60,"usage":{"input_tokens":1,"output_tokens":1}}""");

        Assert.That(stream.Result!.IsError, Is.True);
        Assert.That(stream.Result!.Subtype, Is.EqualTo("error_max_turns"));
    }

    [Test]
    public void ServersReportsFailed()
    {
        var stream = new ClaudeStream("bimopenflow");
        stream.Read("""{"type":"system","subtype":"init","session_id":"s1","mcp_servers":[{"name":"bimopenflow","status":"failed"}]}""");

        Assert.That(stream.Servers, Is.EqualTo(new[] { new McpServerStatus("bimopenflow", "failed") }));
    }
}
