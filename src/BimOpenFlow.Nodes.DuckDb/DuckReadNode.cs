using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>Loads one data file (csv/parquet/json) into a table via DuckDB's
/// readers, cached by file content hash so unchanged files never reload.</summary>
public sealed class DuckReadNode : IFlowNode
{
    public const string Kind = "duck.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("format", ParamKind.Enum, "auto", ["auto", "csv", "parquet", "json"]),
        ],
        "Loads a CSV, Parquet, or JSON file into a table using DuckDB's readers.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track A");
}
