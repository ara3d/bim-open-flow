using System.Collections.Concurrent;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads one BFAST buffer as a one-column table under a primitive element type
/// the caller names, since the container stores none. Cached by file content hash,
/// buffer name, and type.</summary>
public sealed class BfastBufferNode : IFlowNode
{
    public const string Kind = "bfast.buffer";
    public const string ColumnName = "value";

    // TODO: unbounded cache; add eviction if long-lived hosts cycle through many files.
    private static readonly ConcurrentDictionary<string, IDataTable> Cache = new();

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("name", ParamKind.Text),
            new ParamSpec("type", ParamKind.Enum, "float32", BfastOps.ElementTypes),
        ],
        "Reads one buffer of a BFAST file as a table with a single `value` column, interpreting "
        + "its bytes as the given element type (integers widen to long, floats to double). "
        + "An unknown buffer name or a byte length that is not a multiple of the element size is an error.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var path = parameters.RequiredText("path", Kind);
        var name = parameters.RequiredText("name", Kind);
        var type = parameters.GetText("type", "float32");
        if (!File.Exists(path))
            throw new FileNotFoundException($"{Kind}: file not found: {path}", path);
        var table = Cache.GetOrAdd($"{FileHashes.HashFile(path)}:{name}:{type}", _ => Load(path, name, type));
        return [new TableValue(table)];
    }

    private static IDataTable Load(string path, string name, string type)
    {
        var builder = new DataTableBuilder(name);
        builder.AddColumn(BfastOps.ReadValues(path, name, type, Kind), ColumnName, BfastOps.ColumnType(type));
        return builder.Build();
    }
}
