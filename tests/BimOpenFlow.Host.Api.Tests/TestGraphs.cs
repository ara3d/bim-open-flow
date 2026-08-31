using System.Text;
using System.Text.Json;
using Ara3D.DataFlowEngine.TestKit;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Api.Tests;

/// <summary>Shared graphs and HTTP helpers for the API tests.</summary>
public static class TestGraphs
{
    /// <summary>const(42) -> negate, as canonical JSON.</summary>
    public static GraphDocument ConstNegate(string value = "42")
        => Graph
            .Node("c", "test.const", ("kind", "Integer"), ("value", value))
            .Node("n", "test.negate")
            .Connect("c.out", "n.in")
            .Build();

    public static Task<HttpResponseMessage> PutText(string path, string body)
        => ApiTestServer.Client.PutAsync(path,
            new StringContent(body, Encoding.UTF8, "application/json"));

    public static async Task<string> GetOk(string path)
    {
        var response = await ApiTestServer.Client.GetAsync(path);
        Assert.That((int)response.StatusCode, Is.EqualTo(200), path);
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<JsonDocument> GetJson(string path)
        => JsonDocument.Parse(await GetOk(path));

    public static async Task PutAnalysis(string id, GraphDocument doc)
    {
        var response = await PutText($"/api/analyses/{id}", doc.ToCanonicalJson());
        Assert.That((int)response.StatusCode, Is.EqualTo(200));
    }
}
