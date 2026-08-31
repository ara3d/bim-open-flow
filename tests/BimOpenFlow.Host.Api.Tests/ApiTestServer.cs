using Ara3D.DataFlowEngine.TestKit;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Host.Catalog;
using BimOpenFlow.Host.Store;
using Microsoft.AspNetCore.Builder;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>One real Kestrel server on an ephemeral port for the whole assembly,
/// over a temp-dir catalog and store and the TestKit node registry.</summary>
[SetUpFixture]
public sealed class ApiTestServer
{
    public static string RootDir = null!;
    public static string ModelsDir = null!;
    public static HttpClient Client = null!;

    private static WebApplication _app = null!;

    [OneTimeSetUp]
    public async Task StartServer()
    {
        RootDir = Path.Combine(Path.GetTempPath(), "bof-api-tests-" + Guid.NewGuid().ToString("N"));
        ModelsDir = Path.Combine(RootDir, "models");
        Directory.CreateDirectory(ModelsDir);
        File.WriteAllBytes(Path.Combine(ModelsDir, "sample model.bos"), "hello"u8.ToArray());

        var catalog = new ModelCatalog(ModelsDir, Path.Combine(RootDir, "cache"));
        var store = new AnalysisStore(Path.Combine(RootDir, "analyses"));
        _app = ApiServer.Create(catalog, store, TestNodes.Registry);
        _app.Urls.Add("http://127.0.0.1:0");
        await _app.StartAsync();
        Client = new HttpClient { BaseAddress = new Uri(_app.Urls.First()) };
    }

    [OneTimeTearDown]
    public async Task StopServer()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        try
        {
            Directory.Delete(RootDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
