using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Tests;

/// <summary>TKT-83: the text endpoint prints a relation's row count and rows, the way the
/// result peek already does, because HostComposition now supplies the text printer a
/// GraphText.IRelationReader built from the same RelationHostResults it uses for peeking.</summary>
[TestFixture]
public sealed class AnalysisTextRelationsTests
{
    private string _root = null!;
    private HostApp _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-host-text-relations-" + Guid.NewGuid().ToString("N"));
        var modelsDir = Path.Combine(_root, "models");
        Directory.CreateDirectory(modelsDir);
        File.WriteAllText(Path.Combine(modelsDir, "walls.csv"), "id,height\n1,2.5\n2,3.0\n3,4.5\n");

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

    private static GraphDocument RelCsv()
        => GraphDocument.Empty
            .AddNode("src", "rel.csv", 1)
            .SetParam("src", "source", "models")
            .SetParam("src", "path", "walls.csv");

    [Test]
    public async Task Text_PrintsARelationsRowCount()
    {
        var put = await _client.PutAsync("/api/analyses/text-rel",
            new StringContent(RelCsv().ToCanonicalJson(), System.Text.Encoding.UTF8, "application/json"));
        Assert.That((int)put.StatusCode, Is.EqualTo(200), await put.Content.ReadAsStringAsync());

        var text = await _client.GetStringAsync("/api/analyses/text-rel/text");

        // Without a reader this would print only "relation plan <hash>"; with one it also
        // gives the row count and column count, the same shape ValueText.Relation produces
        // for the result peek.
        Assert.That(text, Does.Contain("relation 3 rows x 2 cols"));
    }
}
