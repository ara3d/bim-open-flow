using System.Collections.Concurrent;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Lists the buffers of a BFAST container without reading their bytes:
/// discovery before naming one in bfast.buffer. Cached by file content hash.</summary>
public sealed class BfastReadNode : IFlowNode
{
    public const string Kind = "bfast.read";

    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("buffers", PortType.Table)],
        Params: [new ParamSpec("path", ParamKind.FilePath)],
        "Lists the buffers in a BFAST file: name, byteLength, index (0-based file order). "
        + "BFAST records no element types; bfast.buffer reads one buffer under a type you name.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var table = Cache.GetOrAdd(FileHashes.HashFile(path), _ => Load(path));
        return [new TableValue(table)];
    }

    private static IDataTable Load(string path)
    {
        var ranges = BfastOps.Ranges(path);
        var builder = new DataTableBuilder("buffers");
        builder.AddColumn(ranges.Select(object? (r) => r.Name).ToArray(), "name", typeof(string));
        builder.AddColumn(ranges.Select(object? (r) => r.ByteLength).ToArray(), "byteLength", typeof(long));
        builder.AddColumn(ranges.Select(object? (_, i) => (long)i).ToArray(), "index", typeof(long));
        return builder.Build();
    }
}
