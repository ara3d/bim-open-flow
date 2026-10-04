using System.Net;
using System.Net.Sockets;

namespace BimOpenFlow.Ask;

public static class LoopbackPorts
{
    /// <summary>A TCP port on 127.0.0.1 that was free a moment ago (bind to 0, read, release).</summary>
    public static int Free()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
