using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace BimOpenFlow.Studio.Tests;

/// <summary>A fake chat completion endpoint that replays a fixed script of replies, one per
/// request, and records what it was asked. Shared by AskAgentTests and ChatBackendTests.</summary>
public sealed class ScriptedModel(params string[] replies) : HttpMessageHandler
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
