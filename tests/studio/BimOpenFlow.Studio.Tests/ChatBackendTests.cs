using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenFlow.Ask;
using BimOpenFlow.Host;
using BimOpenMcp.Flow;

namespace BimOpenFlow.Studio.Tests;

/// <summary>ChatBackend wraps AskAgent over an IChatModel as an IAskConversation. The tool loop
/// itself is AskAgentTests' concern; these tests cover what the interface adds: continuing a
/// conversation, isolating separate ones, hiding tools, and stopping at the turn limit.</summary>
public sealed class ChatBackendTests
{
    private string _root = null!;
    private FlowServices _services = null!;
    private McpServer _tools = null!;

    [SetUp]
    public void CreateServices()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-studio-tests-" + Guid.NewGuid().ToString("N"));
        var models = Path.Combine(_root, "models");
        Directory.CreateDirectory(models);
        _services = FlowServices.Create(new HostConfig(
            [models], Path.Combine(_root, "cache"), Path.Combine(_root, "analyses"), Port: 0, Profile: HostConfig.TablesProfile));
        _tools = FlowMcpServer.RegisterTools(
            new McpServer(McpServer.DefaultPort, "test", "0", transport: McpTransport.Http), _services);
    }

    [TearDown]
    public void DeleteRoot()
    {
        _tools.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string ToolCall(string callId, string name, string arguments)
        => new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["finish_reason"] = "tool_calls",
                ["message"] = new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = null,
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["id"] = callId,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = name, ["arguments"] = arguments },
                    }),
                },
            }),
            ["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 20 },
        }.ToJsonString();

    private static string Text(string text)
        => new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["finish_reason"] = "stop",
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text },
            }),
            ["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 20 },
        }.ToJsonString();

    private ChatBackend Backend(ScriptedModel model, IReadOnlySet<string>? hidden = null, int maxTurns = AskAgent.DefaultMaxTurns)
        => new(new AskSetup(_tools, "bimopenflow", hidden ?? new HashSet<string>(StringComparer.Ordinal), maxTurns),
            new OpenAiChat(new HttpClient(model), "sk-test", "gpt-test"));

    private static async Task<AskOutcome> Send(IAskConversation conversation, string user)
        => await conversation.SendAsync(user, _ => Task.CompletedTask, CancellationToken.None);

    [Test]
    public async Task ASecondSendOnOneConversationSendsTheFirstExchangeBack()
    {
        var model = new ScriptedModel(
            Text("first answer"),
            Text("second answer"));
        var conversation = Backend(model).Start("system prompt");

        var first = await Send(conversation, "first request");
        Assert.That(first.Text, Is.EqualTo("first answer"));

        var second = await Send(conversation, "second request");
        Assert.That(second.Text, Is.EqualTo("second answer"));

        var secondRequest = model.Requests[1]["messages"]!.AsArray();
        Assert.That(secondRequest.Select(m => m!["role"]!.GetValue<string>()),
            Is.EqualTo(new[] { "system", "user", "assistant", "user" }));
        Assert.That(secondRequest[1]!["content"]!.GetValue<string>(), Is.EqualTo("first request"));
        Assert.That(secondRequest[2]!["content"]!.GetValue<string>(), Is.EqualTo("first answer"));
        Assert.That(secondRequest[3]!["content"]!.GetValue<string>(), Is.EqualTo("second request"));
    }

    [Test]
    public async Task TwoConversationsFromOneBackendShareNothing()
    {
        var model1 = new ScriptedModel(Text("answer one"));
        var model2 = new ScriptedModel(Text("answer two"));
        var backend1 = Backend(model1);
        var backend2 = Backend(model2);

        var conversationA = backend1.Start("system prompt");
        var conversationB = backend2.Start("system prompt");

        await Send(conversationA, "request A");
        await Send(conversationB, "request B");

        Assert.That(model1.Requests, Has.Count.EqualTo(1), "each conversation talks to its own model");
        Assert.That(model2.Requests, Has.Count.EqualTo(1));
        Assert.That(model1.Requests[0]["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()),
            Is.EqualTo(new[] { "system", "user" }), "no leftover messages from the other conversation");
    }

    [Test]
    public async Task HiddenToolsAreNotOffered()
    {
        var model = new ScriptedModel(Text("ok"));
        var conversation = Backend(model, hidden: new HashSet<string> { "getNodeCatalog", "listDatabases" }).Start("system prompt");

        await Send(conversation, "request");

        var functions = model.Requests[0]["tools"]!.AsArray().Select(t => t!["function"]!["name"]!.GetValue<string>()).ToList();
        Assert.That(functions, Does.Not.Contain("getNodeCatalog").And.Not.Contain("listDatabases"));
        Assert.That(functions, Does.Contain("addNode"));
    }

    [Test]
    public void MaxTurnsStopsTheLoopWithTurnLimitMessage()
    {
        var model = new ScriptedModel(
            ToolCall("c1", "listAnalyses", "{}"),
            ToolCall("c2", "listAnalyses", "{}"),
            Text("never reached"));
        var conversation = Backend(model, maxTurns: 2).Start("system prompt");

        var error = Assert.ThrowsAsync<InvalidOperationException>(() => Send(conversation, "request"));
        Assert.That(error!.Message, Is.EqualTo(AskAgent.TurnLimitMessage(2)));
    }
}
