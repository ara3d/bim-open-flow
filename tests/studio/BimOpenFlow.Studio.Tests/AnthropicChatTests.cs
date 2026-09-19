using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenFlow.Ask;
using BimOpenFlow.Host;
using BimOpenMcp.Flow;

namespace BimOpenFlow.Studio.Tests;

/// <summary>The agent loop through the Claude backend against the real tool server, with the
/// Messages API scripted: covers the request translation (system block, tools, tool results),
/// the reply translation (tool_use to tool_calls, usage, refusal), and replay of the raw blocks.</summary>
public sealed class AnthropicChatTests
{
    private string _root = null!;
    private McpServer _tools = null!;

    [SetUp]
    public void CreateServices()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-anthropic-tests-" + Guid.NewGuid().ToString("N"));
        var models = Path.Combine(_root, "models");
        Directory.CreateDirectory(models);
        var services = FlowServices.Create(new HostConfig(
            [models], Path.Combine(_root, "cache"), Path.Combine(_root, "analyses"), Port: 0, Profile: HostConfig.TablesProfile));
        _tools = FlowMcpServer.RegisterTools(
            new McpServer(McpServer.DefaultPort, "test", "0", transport: McpTransport.Http), services);
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

    private sealed class ScriptedClaude(params string[] replies) : HttpMessageHandler
    {
        public readonly List<JsonObject> Requests = [];
        public readonly List<HttpRequestHeaders> Headers = [];
        private int _next;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject());
            Headers.Add(request.Headers);
            var reply = replies[_next++];
            var status = reply.StartsWith("401", StringComparison.Ordinal) ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
            var body = status == HttpStatusCode.OK ? reply : reply[3..];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string Reply(string stopReason, params JsonObject[] blocks)
        => new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = new JsonArray(blocks),
            ["stop_reason"] = stopReason,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 10, ["cache_read_input_tokens"] = 80, ["cache_creation_input_tokens"] = 10,
                ["output_tokens"] = 20,
            },
        }.ToJsonString();

    private static JsonObject Thinking() => new() { ["type"] = "thinking", ["thinking"] = "", ["signature"] = "sig" };
    private static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };
    private static JsonObject ToolUse(string id, string name, string input)
        => new() { ["type"] = "tool_use", ["id"] = id, ["name"] = name, ["input"] = JsonNode.Parse(input) };

    private AskAgent Agent(ScriptedClaude model)
        => new(_tools, new AnthropicChat(new HttpClient(model), "sk-ant-test", "claude-test") { Effort = "high" });

    private static async Task<(AskOutcome Outcome, List<AskEvent> Events)> Run(AskAgent agent)
    {
        var events = new List<AskEvent>();
        var outcome = await agent.RunAsync("system prompt", "user request", e =>
        {
            events.Add(e);
            return Task.CompletedTask;
        }, CancellationToken.None);
        return (outcome, events);
    }

    [Test]
    public async Task BuildsThroughTheToolsWithTheMessagesApiShape()
    {
        var model = new ScriptedClaude(
            Reply("tool_use", Thinking(), Text("Adding the source."),
                ToolUse("toolu_1", "addNode", """{"id":"ask-test","nodeId":"database","kind":"duck.source"}""")),
            Reply("end_turn", Text("Built it.")));
        var (outcome, events) = await Run(Agent(model));

        Assert.That(outcome.Text, Is.EqualTo("Built it."));
        Assert.That(outcome.Turns, Is.EqualTo(2));
        Assert.That((outcome.InputTokens, outcome.OutputTokens), Is.EqualTo((200L, 40L)), "cached input counts as prompt tokens");
        Assert.That(events.Select(e => (e.Type, e.Name)), Is.EqualTo(new[] { ("text", (string?)null), ("tool", "addNode") }));

        Assert.That(model.Headers[0].GetValues("x-api-key").Single(), Is.EqualTo("sk-ant-test"));
        Assert.That(model.Headers[0].GetValues("anthropic-version").Single(), Is.EqualTo(AnthropicChat.ApiVersion));

        var first = model.Requests[0];
        Assert.That(first["model"]!.GetValue<string>(), Is.EqualTo("claude-test"));
        Assert.That(first["max_tokens"]!.GetValue<int>(), Is.EqualTo(AnthropicChat.MaxTokens));
        Assert.That(first["output_config"]!["effort"]!.GetValue<string>(), Is.EqualTo("high"));
        var system = first["system"]!.AsArray()[0]!;
        Assert.That(system["text"]!.GetValue<string>(), Is.EqualTo("system prompt"));
        Assert.That(system["cache_control"]!["type"]!.GetValue<string>(), Is.EqualTo("ephemeral"));
        Assert.That(first["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "user" }));
        var tool = first["tools"]!.AsArray().OfType<JsonObject>().First(t => t["name"]!.GetValue<string>() == "addNode");
        Assert.That(tool["input_schema"]!["type"]!.GetValue<string>(), Is.EqualTo("object"));
        Assert.That(tool.ContainsKey("function"), Is.False, "no chat-completions wrapper leaks through");

        var second = model.Requests[1]["messages"]!.AsArray();
        Assert.That(second.Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "user", "assistant", "user" }));
        var replayed = second[1]!["content"]!.AsArray();
        Assert.That(replayed.Select(b => b!["type"]!.GetValue<string>()), Is.EqualTo(new[] { "thinking", "text", "tool_use" }),
            "the raw blocks, thinking included, go back unchanged");
        var result = second[2]!["content"]!.AsArray()[0]!;
        Assert.That(result["type"]!.GetValue<string>(), Is.EqualTo("tool_result"));
        Assert.That(result["tool_use_id"]!.GetValue<string>(), Is.EqualTo("toolu_1"));
        Assert.That(result["content"]!.GetValue<string>(), Does.Contain("\"ok\":true"));
    }

    [Test]
    public async Task ParallelToolCallsReturnInOneUserMessage()
    {
        var model = new ScriptedClaude(
            Reply("tool_use",
                ToolUse("toolu_1", "listAnalyses", "{}"),
                ToolUse("toolu_2", "addNode", """{"id":"ask-test","nodeId":"database","kind":"duck.source"}""")),
            Reply("end_turn", Text("done")));
        await Run(Agent(model));

        var messages = model.Requests[1]["messages"]!.AsArray();
        Assert.That(messages, Has.Count.EqualTo(3));
        var results = messages[2]!["content"]!.AsArray();
        Assert.That(results.Select(r => r!["tool_use_id"]!.GetValue<string>()), Is.EqualTo(new[] { "toolu_1", "toolu_2" }));
    }

    [Test]
    public async Task ARefusalBecomesAnAnswerNotAToolCall()
    {
        var refused = JsonNode.Parse(Reply("refusal", ToolUse("toolu_1", "addNode", "{}")))!.AsObject();
        refused["stop_details"] = new JsonObject { ["type"] = "refusal", ["category"] = "cyber" };
        var model = new ScriptedClaude(refused.ToJsonString());
        var (outcome, events) = await Run(Agent(model));

        Assert.That(events, Is.Empty, "the tool_use in a refused turn is not executed");
        Assert.That(outcome.Text, Does.StartWith("The model declined this request (cyber)"));
    }

    [Test]
    public void ApiErrorsCarryTheApiMessage()
    {
        var model = new ScriptedClaude("""401{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""");
        var error = Assert.ThrowsAsync<HttpRequestException>(() => Run(Agent(model)));
        Assert.That(error!.Message, Is.EqualTo("Anthropic 401: invalid x-api-key"));
    }

    [Test]
    public void AssistantMessagesWithoutRawBlocksAreRebuilt()
    {
        var loopShape = new JsonArray(
            new JsonObject { ["role"] = "system", ["content"] = "s" },
            new JsonObject { ["role"] = "user", ["content"] = "u" },
            new JsonObject
            {
                ["role"] = "assistant", ["content"] = "thinking aloud",
                ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = "c1", ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = "evaluate", ["arguments"] = """{"id":"g"}""" },
                }),
            },
            new JsonObject { ["role"] = "tool", ["tool_call_id"] = "c1", ["content"] = "{}" });
        var messages = AnthropicChat.ToMessages(loopShape);
        Assert.That(messages.Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "user", "assistant", "user" }));
        var assistant = messages[1]!["content"]!.AsArray();
        Assert.That(assistant[0]!["text"]!.GetValue<string>(), Is.EqualTo("thinking aloud"));
        Assert.That(assistant[1]!["input"]!["id"]!.GetValue<string>(), Is.EqualTo("g"));
    }

    [Test]
    public void EndpointFollowsTheBaseUrlVariable()
    {
        Assert.That(AnthropicChat.ResolveEndpoint(_ => null), Is.EqualTo(AnthropicChat.DefaultEndpoint));
        Assert.That(AnthropicChat.ResolveEndpoint(n => n == AnthropicChat.BaseUrlVariable ? "https://proxy.local/" : null),
            Is.EqualTo("https://proxy.local/v1/messages"));
    }
}

public sealed class ChatSelectionTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] values)
        => name => values.FirstOrDefault(v => v.Name == name).Value;

    [Test]
    public void AnthropicWinsWhenBothKeysExist()
    {
        var selection = ChatSelection.Resolve(Env(("ANTHROPIC_API_KEY", "a"), ("OPENAI_API_KEY", "o")));
        Assert.That((selection.Provider, selection.Model, selection.Configured),
            Is.EqualTo((AnthropicChat.ProviderName, AnthropicChat.DefaultModel, true)));
    }

    [Test]
    public void OpenAiIsUsedWhenOnlyItsKeyExists()
    {
        var selection = ChatSelection.Resolve(Env(("OPENAI_API_KEY", "o"), ("OPENAI_MODEL", "gpt-5-mini")));
        Assert.That((selection.Provider, selection.Model), Is.EqualTo((OpenAiChat.ProviderName, "gpt-5-mini")));
    }

    [Test]
    public void TheProviderVariableOverridesTheKeyOrder()
    {
        var selection = ChatSelection.Resolve(Env(("ASK_PROVIDER", "openai"), ("ANTHROPIC_API_KEY", "a"), ("OPENAI_API_KEY", "o")));
        Assert.That(selection.Provider, Is.EqualTo(OpenAiChat.ProviderName));
        var missing = ChatSelection.Resolve(Env(("ASK_PROVIDER", "openai"), ("ANTHROPIC_API_KEY", "a")));
        Assert.That(missing.Configured, Is.False, "a named provider without its key is not silently swapped");
    }

    [Test]
    public void NoKeyIsReportedNotThrown()
    {
        var selection = ChatSelection.Resolve(_ => null);
        Assert.That(selection.Problem, Is.EqualTo(ChatSelection.NoKey));
        Assert.Throws<InvalidOperationException>(() => selection.Create(new HttpClient()));
    }

    [Test]
    public void AnUnknownProviderIsReported()
        => Assert.That(ChatSelection.Resolve(Env(("ASK_PROVIDER", "gemini"))).Problem, Does.Contain("gemini"));

    [Test]
    public void CreateReturnsTheMatchingClient()
    {
        var env = Env(("ANTHROPIC_API_KEY", "a"), ("ANTHROPIC_MODEL", "claude-x"));
        var chat = ChatSelection.Resolve(env).Create(new HttpClient(), env);
        Assert.That(chat, Is.InstanceOf<AnthropicChat>());
        Assert.That(chat.Model, Is.EqualTo("claude-x"));
    }
}
