using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads one worksheet of an .xlsx workbook into a table; the first
/// row is the header. Cached by file content hash.</summary>
public sealed class XlsxReadNode : IFlowNode
{
    public const string Kind = "xlsx.read";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("sheet", ParamKind.Text, ""),
        ],
        "Reads a worksheet (named, or the first) from an .xlsx file; row 1 is the header.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
        => throw new NotImplementedException("track B");
}
