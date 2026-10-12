using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>The schema pass's results as wire contracts.</summary>
public static class SchemaMapping
{
    public static PortSchema ToPortSchema(this InferredPort port)
        => new(port.Node, port.Output, port.Status,
            port.Schema?.Columns.Select(c => c.ToDescriptor(port.Kinds)).ToList(),
            port.Reason, port.Errors ?? [], port.Warnings ?? []);
}
