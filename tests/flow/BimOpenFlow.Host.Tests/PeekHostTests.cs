using System.Text;
using System.Text.Json;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;

namespace BimOpenFlow.Host.Tests;

/// <summary>Pins the peek's host contract (TKT-11 C4): a take=0 page counts a wire's rows
/// without transferring any of them, whether the source is a relation or a materialised
/// table, and peeking upstream of a sink node never runs its effect.</summary>
[TestFixture]
public sealed class PeekHostTests
{
    private string _root = null!;
    private HostApp _host = null!;
    private HttpClient _client = null!;
    private string _exportPath = null!;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-host-peek-" + Guid.NewGuid().ToString("N"));
        var modelsDir = Path.Combine(_root, "models");
        Directory.CreateDirectory(modelsDir);
        File.WriteAllText(Path.Combine(modelsDir, "walls.csv"), "id,height\n1,2.5\n2,3.0\n3,4.5\n");
        _exportPath = Path.Combine(_root, "out.csv");

        var config = new HostConfig([modelsDir], Path.Combine(_root, "cache"),
            Path.Combine(_root, "analyses"), Port: 0);
        _host = HostComposition.Build(config);
        await _host.App.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_host.App.Urls.First()) };
    }

    [OneTimeTearDown]
    public async Task StopHost()
    {
        _client.Dispose();
        await _host.App.StopAsync();
        await _host.App.DisposeAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>rel.csv over the models root, into rel.filter, into rel.materialize, into a
    /// CSV sink. The tall-walls fixture pattern from HostHttpTests.TallWalls, extended with
    /// a materialize step and an effect sink so the peek can be checked at every stage.</summary>
    private GraphDocument TallWallsToCsv()
        => GraphDocument.Empty
            .AddNode("src", "rel.csv", 1)
            .SetParam("src", "source", "models")
            .SetParam("src", "path", "walls.csv")
            .AddNode("tall", "rel.filter", 1)
            .SetParam("tall", "expr", "[height] >= 3")
            .AddNode("mat", "rel.materialize", 1)
            .AddNode("out", "sink.exportCsv", 1)
            .SetParam("out", "path", _exportPath)
            .Connect("src.relation", "tall.input")
            .Connect("tall.relation", "mat.input")
            .Connect("mat.table", "out.in");

    [Test]
    public async Task Peek_CountsWithoutRowsAndNeverRunsTheSink()
    {
        var put = await _client.PutAsync("/api/analyses/peek",
            new StringContent(TallWallsToCsv().ToCanonicalJson(), Encoding.UTF8, "application/json"));
        Assert.That((int)put.StatusCode, Is.EqualTo(200), await put.Content.ReadAsStringAsync());

        // A relation's take=0 page counts its rows with none of them, and its columns still
        // come through (RelationHostResults.Slice: LIMIT 0 plus a separate count).
        var tall = await _client.GetAsync("/api/analyses/peek/results/tall/relation?skip=0&take=0");
        Assert.That((int)tall.StatusCode, Is.EqualTo(200), await tall.Content.ReadAsStringAsync());
        using (var slice = JsonDocument.Parse(await tall.Content.ReadAsStringAsync()))
        {
            Assert.That(slice.RootElement.GetProperty("rows").GetArrayLength(), Is.EqualTo(0));
            Assert.That(slice.RootElement.GetProperty("totalRows").GetInt32(), Is.EqualTo(2));
            Assert.That(slice.RootElement.GetProperty("columns").EnumerateArray()
                    .Select(c => c.GetProperty("name").GetString()),
                Is.EqualTo(new[] { "id", "height" }));
        }

        // A materialised table's take=0 page counts the same way (TableValue.ToSlice clamps
        // the row window to zero width but still reports Rows.Count as totalRows).
        var matCount = await _client.GetAsync("/api/analyses/peek/results/mat/table?skip=0&take=0");
        Assert.That((int)matCount.StatusCode, Is.EqualTo(200), await matCount.Content.ReadAsStringAsync());
        using (var slice = JsonDocument.Parse(await matCount.Content.ReadAsStringAsync()))
        {
            Assert.That(slice.RootElement.GetProperty("rows").GetArrayLength(), Is.EqualTo(0));
            Assert.That(slice.RootElement.GetProperty("totalRows").GetInt32(), Is.EqualTo(2));
        }

        // The same port with take=5 is the peek itself: the two tall rows come back.
        var matPeek = await _client.GetAsync("/api/analyses/peek/results/mat/table?skip=0&take=5");
        Assert.That((int)matPeek.StatusCode, Is.EqualTo(200), await matPeek.Content.ReadAsStringAsync());
        using (var slice = JsonDocument.Parse(await matPeek.Content.ReadAsStringAsync()))
        {
            Assert.That(slice.RootElement.GetProperty("rows").GetArrayLength(), Is.EqualTo(2));
            Assert.That(slice.RootElement.GetProperty("totalRows").GetInt32(), Is.EqualTo(2));
        }

        // Peeking the sink's own output before any run is not possible: it has never produced
        // a result, so the host has nothing to page.
        var outResult = await _client.GetAsync("/api/analyses/peek/results/out/out?skip=0&take=5");
        Assert.That((int)outResult.StatusCode, Is.EqualTo(404));

        // Peeking never ran the sink: it is still pending a Run, wrote no file, and recorded
        // no run.
        var state = await _client.GetAsync("/api/analyses/peek/state");
        Assert.That((int)state.StatusCode, Is.EqualTo(200));
        using var update = JsonDocument.Parse(await state.Content.ReadAsStringAsync());
        var outNode = update.RootElement.GetProperty("nodes").EnumerateArray()
            .Single(n => n.GetProperty("nodeId").GetString() == "out");
        Assert.That(outNode.GetProperty("status").GetString(), Is.EqualTo("EffectPending"));
        Assert.That(File.Exists(_exportPath), Is.False);

        var runs = await _client.GetAsync("/api/analyses/peek/runs");
        Assert.That((int)runs.StatusCode, Is.EqualTo(200));
        using var runsList = JsonDocument.Parse(await runs.Content.ReadAsStringAsync());
        Assert.That(runsList.RootElement.GetArrayLength(), Is.EqualTo(0));
    }
}
