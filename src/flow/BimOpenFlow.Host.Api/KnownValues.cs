using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Host.Api;

/// <summary>The values a node's non-table outputs hold that the schema pass can know without reading
/// data: the constants of the Value pack (value.*, which have no inputs and only echo a parameter) and the
/// path of duck.source as a Database value. Every other output is unknown (null).</summary>
internal static class KnownValues
{
    public const string ValuePrefix = "value.";
    public const string DuckSourceKind = "duck.source";

    public static IReadOnlyList<FlowValue?> Of(IFlowNode node, IReadOnlyDictionary<string, string> parameters)
    {
        var spec = node.Spec;
        if (spec.Kind == DuckSourceKind)
            return [new DatabaseValue(parameters.GetValueOrDefault("path") ?? "", "")];
        if (!spec.Kind.StartsWith(ValuePrefix, StringComparison.Ordinal) || spec.Inputs.Count > 0)
            return Unknown(spec);
        try
        {
            return node.Eval(Quiet.Instance, [], new ParamValues(parameters))
                .Select(v => (FlowValue?)v).ToList();
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
        {
            return Unknown(spec);
        }
    }

    private static IReadOnlyList<FlowValue?> Unknown(NodeSpec spec)
        => spec.Outputs.Select(_ => (FlowValue?)null).ToList();

    private sealed class Quiet : IEvalContext
    {
        public static readonly Quiet Instance = new();
        public bool IsRun => false;
        public CancellationToken Cancellation => CancellationToken.None;

        public void Warn(string message)
        {
        }
    }
}
