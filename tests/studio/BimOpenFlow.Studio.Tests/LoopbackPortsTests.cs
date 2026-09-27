using System.Net;
using System.Net.Sockets;
using System.Text;
using Ara3D.MCP;
using BimOpenFlow.Ask;

namespace BimOpenFlow.Studio.Tests;

/// <summary>LoopbackPorts.Free() gives out a port a real McpServer can bind and a real
/// HttpClient can reach, which is all ClaudeCliBackend (C7) needs from it.</summary>
public sealed class LoopbackPortsTests
{
    [Test]
    public void ReturnsAPortInTheDynamicRange()
    {
        var port = LoopbackPorts.Free();
        Assert.That(port, Is.InRange(1024, 65535));
    }

    [Test]
    public void TwoCallsInARowCanBothBeBound()
    {
        var first = LoopbackPorts.Free();
        var second = LoopbackPorts.Free();

        using var firstListener = new TcpListener(IPAddress.Loopback, first);
        using var secondListener = new TcpListener(IPAddress.Loopback, second);
        firstListener.Start();
        secondListener.Start();

        Assert.That(firstListener.Server.IsBound, Is.True);
        Assert.That(secondListener.Server.IsBound, Is.True);
    }

    [Test]
    public async Task AnMcpServerBoundToAFreePortAnswersToolsListOverHttp()
    {
        var port = LoopbackPorts.Free();
        using var server = new McpServer(port, "loopback-ports-test", "0", transport: McpTransport.Http);
        server.Start();

        using var http = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, server.Url)
        {
            Content = new StringContent(
                """{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""",
                Encoding.UTF8, "application/json"),
        };
        var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body, Does.Contain("\"tools\""));
    }
}
