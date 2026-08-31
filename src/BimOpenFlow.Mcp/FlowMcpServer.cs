using Ara3D.MCP;
using BimOpenFlow.Host;
using BimOpenFlow.Host.Api;

namespace BimOpenFlow.Mcp;

/// <summary>
/// What every tool operates on: the one host wiring (catalog, store, registry)
/// plus a standing evaluation session per analysis — the same semantics the
/// HTTP API uses, so agents and humans manipulate graphs through the same
/// operations.
/// </summary>
public sealed record FlowServices(HostServices Host, AnalysisSessions Sessions)
{
    public static FlowServices Create(HostConfig config)
    {
        var host = HostComposition.BuildServices(config);
        return new(host, new AnalysisSessions(host.Store, host.Registry));
    }
}

public static class FlowMcpServer
{
    public const string ServerName = "bimopenflow";
    public const string ServerVersion = "0.1.0";

    /// <summary>Builds a server with the whole BimOpenFlow tool surface registered
    /// against one set of services. The caller owns both and must dispose the server.</summary>
    public static McpServer Create(FlowServices services, McpTransport transport, int port = McpServer.DefaultPort)
        => RegisterTools(new McpServer(port, ServerName, ServerVersion, transport: transport), services);

    public static McpServer RegisterTools(McpServer mcp, FlowServices services)
        => mcp
            .RegisterDocumentTools(services)
            .RegisterEditTools(services)
            .RegisterEvalTools(services);
}
