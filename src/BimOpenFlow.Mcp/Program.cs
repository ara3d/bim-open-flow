using System.Text;
using Ara3D.MCP;
using BimOpenFlow.Host;
using BimOpenFlow.Mcp;

// Stdio is the default because that is how MCP clients launch a server. Under it stdout is the
// protocol stream, so every diagnostic goes to stderr. Pass --http [port] to listen instead.
// Host settings (--models/--cache/--store) resolve exactly as for bimopenflow-host.
var useHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
var port = ParsePort(args);

Console.OutputEncoding = new UTF8Encoding(false);

var config = HostConfig.Resolve(StripHttpArgs(args), Environment.CurrentDirectory);
var services = FlowServices.Create(config);
using var mcp = FlowMcpServer.Create(services, useHttp ? McpTransport.Http : McpTransport.Stdio, port);

mcp.Start();

if (useHttp)
{
    Console.Error.WriteLine($"{FlowMcpServer.ServerName} listening at {mcp.Url}");
    Console.Error.WriteLine("Press Ctrl+C to stop.");
    using var stop = new ManualResetEventSlim(false);
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        // ReSharper disable once AccessToDisposedClosure
        stop.Set();
    };
    stop.Wait();
}
else
{
    Console.Error.WriteLine($"{FlowMcpServer.ServerName} {FlowMcpServer.ServerVersion} on stdio.");
    mcp.WaitForShutdown();
}

return 0;

static int ParsePort(string[] arguments)
{
    var index = Array.FindIndex(arguments, a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var value)
        ? value
        : McpServer.DefaultPort;
}

static string[] StripHttpArgs(string[] arguments)
{
    var index = Array.FindIndex(arguments, a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));
    if (index < 0)
        return arguments;
    var count = index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out _) ? 2 : 1;
    return arguments.Where((_, i) => i < index || i >= index + count).ToArray();
}
