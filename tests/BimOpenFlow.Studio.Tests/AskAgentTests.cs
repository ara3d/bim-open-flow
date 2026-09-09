using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenFlow.Host;
using BimOpenFlow.Mcp;

namespace BimOpenFlow.Studio.Tests;

/// <summary>The agent loop against the real MCP tool server over fresh temp
/// directories, with the model scripted: each reply is what the API would
/// return, so the tests cover the request shape, tool routing, and reporting.</summary>
public sealed class AskAgentTests
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

    private sealed class ScriptedModel(params string[] replies) : HttpMessageHandler
    {
        public readonly List<JsonObject> Requests = [];
        public readonly List<string?> BearerTokens = [];
        private int _next;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject());
            BearerTokens.Add(request.Headers.Authorization?.Parameter);
            var reply = replies[_next++];
            var status = reply.StartsWith("401", StringComparison.Ordinal) ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
            var body = status == HttpStatusCode.OK ? reply : reply[3..];
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
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

    private AskAgent Agent(ScriptedModel model, int maxTurns = AskAgent.DefaultMaxTurns)
        => new(_tools, new OpenAiChat(new HttpClient(model), "sk-test", "gpt-test"), maxTurns);

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
    public async Task BuildsThroughTheToolsAndReportsEachStep()
    {
        var model = new ScriptedModel(
            ToolCall("c1", "addNode", """{"id":"ask-test","nodeId":"database","kind":"duck.source"}"""),
            ToolCall("c2", "evaluate", """{"id":"ask-test"}"""),
            Text("Built it."));
        var (outcome, events) = await Run(Agent(model));

        Assert.That(outcome.Text, Is.EqualTo("Built it."));
        Assert.That(outcome.Turns, Is.EqualTo(3));
        Assert.That((outcome.InputTokens, outcome.OutputTokens), Is.EqualTo((300L, 60L)));
        Assert.That(_services.Host.Store.Exists("ask-test"), Is.True, "the tool call reached the store");

        Assert.That(events.Select(e => (e.Type, e.Name, e.Ok)),
            Is.EqualTo(new[] { ("tool", "addNode", (bool?)true), ("tool", "evaluate", (bool?)true) }));
        Assert.That(events[1].Summary, Does.Contain("database"), "evaluate summarises per node");

        Assert.That(model.BearerTokens, Is.All.EqualTo("sk-test"));
        var first = model.Requests[0];
        Assert.That(first["model"]!.GetValue<string>(), Is.EqualTo("gpt-test"));
        var functions = first["tools"]!.AsArray().Select(t => t!["function"]!["name"]!.GetValue<string>()).ToList();
        Assert.That(functions, Does.Contain("addNode").And.Contain("describeDatabase").And.Contain("getResult"));
        Assert.That(first["messages"]!.AsArray().Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "system", "user" }));

        var second = model.Requests[1]["messages"]!.AsArray();
        Assert.That(second.Select(m => m!["role"]!.GetValue<string>()), Is.EqualTo(new[] { "system", "user", "assistant", "tool" }));
        Assert.That(second[2]!["tool_calls"]!.AsArray(), Has.Count.EqualTo(1), "the assistant message is echoed with its calls");
        Assert.That(second[3]!["tool_call_id"]!.GetValue<string>(), Is.EqualTo("c1"));
        Assert.That(second[3]!["content"]!.GetValue<string>(), Does.Contain("\"ok\":true"));
    }

    [Test]
    public async Task AFailedToolCallIsReportedAndSentBack()
    {
        var model = new ScriptedModel(
            ToolCall("c1", "addNode", """{"id":"ask-test","nodeId":"x","kind":"no.such"}"""),
            Text("That kind does not exist."));
        var (outcome, events) = await Run(Agent(model));

        Assert.That(outcome.Text, Is.EqualTo("That kind does not exist."));
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].Ok, Is.False);
        Assert.That(events[0].Summary, Does.Contain("Unknown node kind"));
        var toolMessage = model.Requests[1]["messages"]!.AsArray()[3]!;
        Assert.That(toolMessage["content"]!.GetValue<string>(), Does.Contain("\"ok\":false"));
    }

    [Test]
    public async Task AnIdenticalRepeatedCallIsFlaggedToTheModel()
    {
        var model = new ScriptedModel(
            ToolCall("c1", "listAnalyses", "{}"),
            ToolCall("c2", "listAnalyses", "{}"),
            ToolCall("c3", "addNode", """{"id":"ask-test","nodeId":"database","kind":"duck.source"}"""),
            Text("done"));
        var (_, events) = await Run(Agent(model));

        Assert.That(events[0].Summary, Does.Not.Contain("repeated"));
        Assert.That(events[1].Summary, Does.EndWith("(repeated, unchanged)"));
        Assert.That(events[2].Summary, Does.Not.Contain("repeated"), "a different call resets the check");
        var secondToolMessage = model.Requests[2]["messages"]!.AsArray().Last()!;
        Assert.That(secondToolMessage["content"]!.GetValue<string>(), Does.Contain("same call as before"));
        var firstToolMessage = model.Requests[1]["messages"]!.AsArray().Last()!;
        Assert.That(firstToolMessage["content"]!.GetValue<string>(), Does.Not.Contain("same call as before"));
    }

    [Test]
    public void UnparseableArgumentsBecomeAnEmptyCallNotACrash()
    {
        var model = new ScriptedModel(ToolCall("c1", "addNode", "not json"), Text("ok"));
        Assert.DoesNotThrowAsync(async () =>
        {
            var (_, events) = await Run(Agent(model));
            Assert.That(events[0].Ok, Is.False, "addNode without arguments is a protocol error, reported not thrown");
        });
    }

    [Test]
    public void StopsAfterTheTurnLimit()
    {
        var model = new ScriptedModel(
            ToolCall("c1", "listAnalyses", "{}"),
            ToolCall("c2", "listAnalyses", "{}"),
            Text("never reached"));
        var error = Assert.ThrowsAsync<InvalidOperationException>(() => Run(Agent(model, maxTurns: 2)));
        Assert.That(error!.Message, Does.Contain("2 model turns"));
    }

    [Test]
    public void ApiErrorsCarryTheApiMessage()
    {
        var model = new ScriptedModel("""401{"error":{"message":"Incorrect API key provided"}}""");
        var error = Assert.ThrowsAsync<HttpRequestException>(() => Run(Agent(model)));
        Assert.That(error!.Message, Is.EqualTo("OpenAI 401: Incorrect API key provided"));
    }

    [Test]
    public void SummariesAreOneLinePerToolKind()
    {
        var evaluated = JsonNode.Parse("""{"nodes":[{"nodeId":"a","status":"Ok"},{"nodeId":"b","status":"Error","error":"boom"}]}""");
        Assert.That(AskAgent.Summarize("evaluate", evaluated), Is.EqualTo("1 Ok; b Error: boom"));
        var allOk = JsonNode.Parse("""{"nodes":[{"nodeId":"a","status":"Ok"},{"nodeId":"b","status":"Ok"}]}""");
        Assert.That(AskAgent.Summarize("evaluate", allOk), Is.EqualTo("2 nodes Ok"));
        var rows = JsonNode.Parse("""{"columns":[{"name":"Mark"},{"name":"Storey"}],"rows":[],"totalRows":142}""");
        Assert.That(AskAgent.Summarize("getResult", rows), Is.EqualTo("142 rows; columns Mark, Storey"));
        var summary = JsonNode.Parse("""{"tables":[{"name":"door","columns":["a"]},{"name":"storey","columns":["b"]}]}""");
        Assert.That(AskAgent.Summarize("describeDatabase", summary), Is.EqualTo("2 tables"));
        var typed = JsonNode.Parse("""{"tables":[{"name":"door","columns":[{"name":"a","type":"INTEGER"},{"name":"b","type":"VARCHAR"}]}]}""");
        Assert.That(AskAgent.Summarize("describeDatabase", typed), Is.EqualTo("door has 2 columns"));
        Assert.That(AskAgent.Summarize("addNode", JsonNode.Parse("""{"id":"x","graphHash":"abc"}""")), Is.EqualTo("saved"));
        Assert.That(AskAgent.Summarize("listAnalyses", JsonNode.Parse("""{"id":"x","name":"y","graphHash":"abc"}""")), Does.StartWith("{"));
    }
}

public sealed class AskIdsTests
{
    [Test]
    public void KeepsTheContentWordsOfTheRequest()
        => Assert.That(AskIds.For("How many rooms are on each storey? Sort by count, largest first.", _ => false),
            Is.EqualTo("ask-rooms-storey-sort-count-largest"));

    [Test]
    public void SuffixesWhenTaken()
    {
        var taken = new HashSet<string> { "ask-rooms", "ask-rooms-2" };
        Assert.That(AskIds.For("rooms", taken.Contains), Is.EqualTo("ask-rooms-3"));
    }

    [Test]
    public void EmptyOrStopWordOnlyRequestsStillGetAnId()
        => Assert.That(AskIds.For("show me the", _ => false), Is.EqualTo("ask-graph"));

    [Test]
    public void LongRequestsAreCut()
    {
        var id = AskIds.For("extraordinarily complicated multidimensional visualisation pipeline description", _ => false);
        Assert.That(id.Length, Is.LessThanOrEqualTo(48));
        Assert.That(id, Does.StartWith("ask-extraordinarily"));
    }
}

public sealed class OpenAiChatKeyTests
{
    [Test]
    public void EnvironmentVariableWins()
        => Assert.That(OpenAiChat.ResolveApiKey(name => name == OpenAiChat.KeyVariable ? " sk-a " : "ignored.txt"), Is.EqualTo("sk-a"));

    [Test]
    public void KeyFileGivesItsFirstNonEmptyLine()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "\n  sk-from-file  \nsecond line\n");
            Assert.That(OpenAiChat.ResolveApiKey(name => name == OpenAiChat.KeyFileVariable ? file : null), Is.EqualTo("sk-from-file"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Test]
    public void NothingConfiguredIsNull()
        => Assert.That(OpenAiChat.ResolveApiKey(_ => null), Is.Null);

    [Test]
    public void MissingKeyFileIsAnError()
        => Assert.Throws<FileNotFoundException>(() => OpenAiChat.ResolveApiKey(name => name == OpenAiChat.KeyFileVariable ? "Z:/no/such/key.txt" : null));

    [Test]
    public void ModelDefaultsAndOverrides()
    {
        Assert.That(OpenAiChat.ResolveModel(_ => null), Is.EqualTo(OpenAiChat.DefaultModel));
        Assert.That(OpenAiChat.ResolveModel(name => name == OpenAiChat.ModelVariable ? " gpt-5-mini " : null), Is.EqualTo("gpt-5-mini"));
    }
}
