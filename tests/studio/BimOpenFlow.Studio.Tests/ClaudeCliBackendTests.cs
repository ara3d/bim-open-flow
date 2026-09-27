using System.Text.Json.Nodes;
using Ara3D.MCP;
using BimOpenFlow.Ask;
using BimOpenFlow.Host;
using BimOpenMcp.Flow;

namespace BimOpenFlow.Studio.Tests;

/// <summary>ClaudeCliBackend against the real flow tool server, over HTTP on a free loopback
/// port, and the fake claude.cmd in tests/studio/fake-claude (see its README.md for the script
/// shape). No account, no network call to Anthropic, and no real claude process is used.</summary>
public sealed class ClaudeCliBackendTests
{
    private string _root = null!;
    private string _workDir = null!;
    private FlowServices _services = null!;
    private McpServer _tools = null!;

    private static string FakeClaudePath => Path.Combine(AppContext.BaseDirectory, "fake-claude", "claude.cmd");

    [SetUp]
    public void CreateServices()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-studio-tests-" + Guid.NewGuid().ToString("N"));
        var models = Path.Combine(_root, "models");
        Directory.CreateDirectory(models);
        _services = FlowServices.Create(new HostConfig(
            [models], Path.Combine(_root, "cache"), Path.Combine(_root, "analyses"), Port: 0, Profile: HostConfig.TablesProfile));
        _tools = FlowMcpServer.RegisterTools(
            new McpServer(LoopbackPorts.Free(), "test", "0", transport: McpTransport.Http), _services);
        _workDir = Path.Combine(_root, "claude-cli");
        Directory.CreateDirectory(_workDir);
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

    private ClaudeCliBackend Backend(IReadOnlySet<string>? hidden = null, int maxTurns = 60, string? executable = null,
        TimeSpan? timeout = null)
        => new(new AskSetup(_tools, "bimopenflow", hidden ?? new HashSet<string>(StringComparer.Ordinal), maxTurns),
            new ClaudeCliSettings(executable ?? FakeClaudePath, "claude-haiku-4-5-20251001", "medium", _workDir)
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(30),
            });

    private void WriteScript(string json)
        => File.WriteAllText(Path.Combine(_workDir, "fake-claude-script.json"), json);

    private List<JsonObject> ReadCallLog()
        => File.ReadAllLines(Path.Combine(_workDir, "fake-claude-calls.jsonl"))
            .Where(line => line.Trim().Length > 0)
            .Select(line => (JsonObject)JsonNode.Parse(line)!)
            .ToList();

    private static Task<AskOutcome> Send(IAskConversation conversation, string user, List<AskEvent>? events = null)
        => conversation.SendAsync(user, e =>
        {
            events?.Add(e);
            return Task.CompletedTask;
        }, CancellationToken.None);

    [Test]
    public async Task AScriptOfAddNodeEvaluateAndResultYieldsTwoToolEventsAndSavesTheAnalysis()
    {
        WriteScript("""
            {"runs":[{"steps":[
                {"call":"addNode","args":{"id":"ask-test","nodeId":"database","kind":"duck.source"}},
                {"call":"evaluate","args":{"id":"ask-test"}},
                {"result":"Built it."}
            ]}]}
            """);
        var conversation = Backend().Start("system prompt");
        var events = new List<AskEvent>();

        var outcome = await Send(conversation, "build it", events);

        Assert.That(outcome.Text, Is.EqualTo("Built it."));
        Assert.That(events.Select(e => (e.Type, e.Name, e.Ok)),
            Is.EqualTo(new[] { ("tool", "addNode", (bool?)true), ("tool", "evaluate", (bool?)true) }));
        Assert.That(_services.Host.Store.Exists("ask-test"), Is.True, "the tool call reached the store");
    }

    [Test]
    public async Task TheSecondSendAsyncPassesResumeWithTheFirstSessionId()
    {
        WriteScript("""
            {"runs":[
                {"steps":[{"result":"first answer"}]},
                {"steps":[{"result":"second answer"}]}
            ]}
            """);
        var conversation = Backend().Start("system prompt");

        var first = await Send(conversation, "first request");
        Assert.That(first.Text, Is.EqualTo("first answer"));
        var second = await Send(conversation, "second request");
        Assert.That(second.Text, Is.EqualTo("second answer"));

        var secondArgs = ReadCallLog()[1]["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToList();
        var resumeIndex = secondArgs.IndexOf("--resume");
        Assert.That(resumeIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(secondArgs[resumeIndex + 1], Is.EqualTo("fake-session-0"));
    }

    [Test]
    public async Task TheCallLogHoldsStdinTheSystemFileHoldsTheSystemTextAndNoAnthropicOrClaudeVariableReachesTheChild()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "sk-should-not-leak");
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        try
        {
            WriteScript("""{"runs":[{"steps":[{"result":"ok"}]}]}""");
            var conversation = Backend().Start("the system prompt text");

            await Send(conversation, "the user text");

            var call = ReadCallLog()[0];
            Assert.That(call["stdin"]!.GetValue<string>(), Is.EqualTo("the user text"));
            Assert.That(call["env"]!.AsArray(), Is.Empty, "no ANTHROPIC_ or CLAUDE name reached the child");

            var args = call["args"]!.AsArray().Select(a => a!.GetValue<string>()).ToList();
            var systemFile = args[args.IndexOf("--system-prompt-file") + 1];
            Assert.That(File.ReadAllText(systemFile), Is.EqualTo("the system prompt text"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            Environment.SetEnvironmentVariable("CLAUDECODE", null);
        }
    }

    [Test]
    public void AnErrorResultThrowsClaudeCodeWithTheResultText()
    {
        WriteScript("""{"runs":[{"steps":[{"error":"Not logged in · Please run /login"}]}]}""");
        var conversation = Backend().Start("system prompt");

        var error = Assert.ThrowsAsync<ClaudeCliException>(() => Send(conversation, "hi"));
        Assert.That(error!.Message, Is.EqualTo("Claude Code: Not logged in · Please run /login"));
    }

    [Test]
    public void ANonZeroExitWithNoResultThrowsWithTheCodeAndTheStderrTail()
    {
        WriteScript("""{"runs":[{"steps":[{"exit":3,"stderr":"boom"}]}]}""");
        var conversation = Backend().Start("system prompt");

        var error = Assert.ThrowsAsync<ClaudeCliException>(() => Send(conversation, "hi"));
        Assert.That(error!.Message, Does.Contain("code 3").And.Contain("boom"));
    }

    [Test]
    public void AMaxTurnsResultThrowsTheTurnLimitMessage()
    {
        WriteScript("""{"runs":[{"steps":[{"maxTurns":true}]}]}""");
        var conversation = Backend(maxTurns: 42).Start("system prompt");

        var error = Assert.ThrowsAsync<ClaudeCliException>(() => Send(conversation, "hi"));
        Assert.That(error!.Message, Is.EqualTo(AskAgent.TurnLimitMessage(42)));
    }

    [Test]
    public void AStoppedToolServerGivesCouldNotConnect()
    {
        _tools.Start();
        var backend = Backend();
        _tools.Stop();
        WriteScript("""{"runs":[{"steps":[{"result":"unreachable"}]}]}""");
        var conversation = backend.Start("system prompt");

        var error = Assert.ThrowsAsync<ClaudeCliException>(() => Send(conversation, "hi"));
        Assert.That(error!.Message, Does.Contain("could not connect"));
    }

    [Test]
    public void AMissingExecutableGivesCouldNotStart()
    {
        WriteScript("""{"runs":[{"steps":[{"result":"unreachable"}]}]}""");
        var conversation = Backend(executable: Path.Combine(_root, "no-such-claude.cmd")).Start("system prompt");

        var error = Assert.ThrowsAsync<ClaudeCliException>(() => Send(conversation, "hi"));
        Assert.That(error!.Message, Does.Contain("Could not start"));
    }

    [Test]
    public async Task ASleepStepWithACancelledTokenEndsWithOperationCanceledExceptionAndKillsTheFakeProcess()
    {
        WriteScript("""{"runs":[{"steps":[{"sleep":10000}]}]}""");
        var conversation = Backend(timeout: TimeSpan.FromMinutes(10)).Start("system prompt");
        var heartbeatFile = Path.Combine(_workDir, "fake-claude-heartbeat.txt");
        using var cts = new CancellationTokenSource();

        var sendTask = conversation.SendAsync("hi", _ => Task.CompletedTask, cts.Token);

        await WaitForHeartbeatAsync(heartbeatFile, TimeSpan.FromSeconds(5));
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await sendTask);

        // The kill is async: give it a moment to land, then check the heartbeat has stopped
        // advancing across a further interval (rather than comparing to a tick read before
        // cancellation, which races the fake's own 100ms timer).
        await Task.Delay(300);
        var firstTick = File.Exists(heartbeatFile) ? File.ReadAllText(heartbeatFile) : "";
        await Task.Delay(500);
        var laterTick = File.Exists(heartbeatFile) ? File.ReadAllText(heartbeatFile) : "";
        Assert.That(laterTick, Is.EqualTo(firstTick), "the fake process was killed, so its heartbeat stopped advancing");
    }

    private static async Task<string> WaitForHeartbeatAsync(string path, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (File.Exists(path))
            {
                var text = File.ReadAllText(path);
                if (text.Length > 0)
                    return text;
            }
            await Task.Delay(50);
        }
        throw new TimeoutException("The fake process never started ticking its heartbeat file.");
    }
}
