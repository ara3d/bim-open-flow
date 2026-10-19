using Microsoft.AspNetCore.Builder;

namespace BimOpenFlow.Host.Tests;

/// <summary>The web folder a release serves beside /api: pages, folder indexes, and file types.</summary>
[TestFixture]
public sealed class StaticWebTests
{
    private string _root = null!;
    private HostApp _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        _root = Path.Combine(Path.GetTempPath(), "bof-static-web-" + Guid.NewGuid().ToString("N"));
        var web = Path.Combine(_root, "web");
        Directory.CreateDirectory(Path.Combine(web, "lists"));
        File.WriteAllText(Path.Combine(web, "index.html"), "<p>home</p>");
        File.WriteAllText(Path.Combine(web, "lists", "index.json"), """["a.notebook.json"]""");
        File.WriteAllText(Path.Combine(web, "graphs.md"), "# graphs");
        File.WriteAllBytes(Path.Combine(web, "m.bos"), [1, 2, 3]);

        var config = new HostConfig([Path.Combine(_root, "models")], Path.Combine(_root, "cache"),
            Path.Combine(_root, "analyses"), Port: 0, WebDir: web);
        _host = HostComposition.Build(config, HostComposition.Tables);
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

    [Test]
    public async Task Root_AnswersIndexHtml()
        => Assert.That(await _client.GetStringAsync("/"), Is.EqualTo("<p>home</p>"));

    [Test]
    public async Task Folder_AnswersIndexJson_AsJson()
    {
        var response = await _client.GetAsync("/lists/");
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("""["a.notebook.json"]"""));
    }

    [TestCase("/graphs.md", "text/markdown")]
    [TestCase("/m.bos", "application/octet-stream")]
    public async Task Files_CarryTheirMediaType(string path, string mediaType)
    {
        var response = await _client.GetAsync(path);
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo(mediaType));
    }

    [Test]
    public async Task Api_StillAnswers()
        => Assert.That((await _client.GetAsync("/api/analyses")).IsSuccessStatusCode, Is.True);

    [Test]
    public void WebArgument_And_Setting_Resolve()
    {
        var config = HostConfig.Default(".").ApplyArgs(["--web", "pages"]);
        Assert.That(config.WebDir, Is.EqualTo("pages"));
        Assert.That(HostConfig.Default(".").WebDir, Is.Null);
    }

    [Test]
    public void MissingFolder_AddsNothing()
        => Assert.That(StaticWeb.Use(WebApplication.CreateBuilder().Build(), Path.Combine(_root, "none")), Is.False);
}
