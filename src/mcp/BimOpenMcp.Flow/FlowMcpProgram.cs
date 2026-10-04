using System.Text;
using Ara3D.MCP;
using BimOpenFlow.Host;

namespace BimOpenMcp.Flow;

/// <summary>The MCP server's main routine over a given profile set, so a front end that
/// composes more profiles (the studio's `mcp` verb) serves them through the same tools.</summary>
public static class FlowMcpProgram
{
    /// <summary>Stdio is the default because that is how MCP clients launch a server. Under it
    /// stdout is the protocol stream, so every diagnostic goes to stderr. Pass --http [port] to
    /// listen instead. Host settings (--models/--cache/--store/--profile) resolve exactly as for
    /// the host.</summary>
    public static int Run(string[] args, HostProfiles profiles)
    {
        var useHttp = args.Contains("--http", StringComparer.OrdinalIgnoreCase);
        var port = ParsePort(args);

        Console.OutputEncoding = new UTF8Encoding(false);

        var config = HostConfig.Resolve(StripHttpArgs(args), Environment.CurrentDirectory, profiles);
        var services = FlowServices.Create(config, profiles[config.Profile]);
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
    }

    private static int ParsePort(string[] arguments)
    {
        var index = Array.FindIndex(arguments, a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out var value)
            ? value
            : McpServer.DefaultPort;
    }

    private static string[] StripHttpArgs(string[] arguments)
    {
        var index = Array.FindIndex(arguments, a => a.Equals("--http", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return arguments;
        var count = index + 1 < arguments.Length && int.TryParse(arguments[index + 1], out _) ? 2 : 1;
        return arguments.Where((_, i) => i < index || i >= index + count).ToArray();
    }
}
