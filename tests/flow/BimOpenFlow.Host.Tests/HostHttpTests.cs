using System.Text;
using System.Text.Json;
using Ara3D.NodeGraph;
using BimOpenFlow.Host;

namespace BimOpenFlow.Host.Tests;

/// <summary>Full HTTP smoke over the real composition: real node packs, real
/// Kestrel on an ephemeral port, temp catalog/store directories.</summary>
[TestFixture]
public sealed class HostHttpTests
{
    private string _root = null!;
    private HostApp _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-host-http-" + Guid.NewGuid().ToString("N"));
        var modelsDir = Path.Combine(_root, "models");
        Directory.CreateDirectory(modelsDir);
        File.WriteAllBytes(Path.Combine(modelsDir, "sample.bos"), "hello"u8.ToArray());

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

    /// <summary>A tiny graph of real nodes that needs no model file.</summary>
    private static GraphDocument CameraSort()
        => GraphDocument.Empty
            .AddNode("cam", "view3d.camera", 1)
            .SetParam("cam", "name", "front")
            .AddNode("sort", "table.sort", 1)
            .SetParam("sort", "by", "name")
            .Connect("cam.camera", "sort.table");

    [Test]
    public async Task PutAnalysis_ThenState_EndToEnd()
    {
        var put = await _client.PutAsync("/api/analyses/smoke",
            new StringContent(CameraSort().ToCanonicalJson(), Encoding.UTF8, "application/json"));
        Assert.That((int)put.StatusCode, Is.EqualTo(200));

        var state = await _client.GetAsync("/api/analyses/smoke/state");
        Assert.That((int)state.StatusCode, Is.EqualTo(200));
        using var update = JsonDocument.Parse(await state.Content.ReadAsStringAsync());
        var statuses = update.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("status").GetString())
            .ToList();
        Assert.That(statuses, Is.EqualTo(new[] { "Ok", "Ok" }));

        var result = await _client.GetAsync("/api/analyses/smoke/results/sort/table");
        Assert.That((int)result.StatusCode, Is.EqualTo(200));
        using var slice = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
        Assert.That(slice.RootElement.GetProperty("totalRows").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task Models_ListAndBosBytes()
    {
        var list = await _client.GetAsync("/api/models");
        Assert.That((int)list.StatusCode, Is.EqualTo(200));
        using var models = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var id = models.RootElement.EnumerateArray().Single().GetProperty("id").GetString();

        var bos = await _client.GetAsync($"/api/models/{id}/bos");
        var bytes = await bos.Content.ReadAsByteArrayAsync();
        Assert.Multiple(() =>
        {
            Assert.That((int)bos.StatusCode, Is.EqualTo(200));
            Assert.That(bos.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/octet-stream"));
            Assert.That(bytes, Is.EqualTo("hello"u8.ToArray()));
        });
    }

    [Test]
    public async Task Models_UnknownBosIs404()
        => Assert.That((int)(await _client.GetAsync("/api/models/no-such/bos")).StatusCode, Is.EqualTo(404));

    [Test]
    public async Task NodeCatalog_ServesAllPacks()
    {
        var response = await _client.GetAsync("/api/catalog/nodes");
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        using var catalog = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var kinds = catalog.RootElement.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("kind").GetString())
            .ToList();
        Assert.That(kinds, Is.SupersetOf(new[] { "bos.load", "view3d.instances", "check.rule", "sink.exportCsv" }));
    }
}
