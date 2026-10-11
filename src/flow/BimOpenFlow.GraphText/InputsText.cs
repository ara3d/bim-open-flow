using Ara3D.DataFlowEngine;
using Ara3D.NodeGraph;

namespace BimOpenFlow.GraphText;

/// <summary>The graph-inputs header line: <c>inputs name: Kind = "default", ...;</c>, printed
/// before the nodes. When the evaluation used a value other than the default, the entry adds
/// <c>now "value"</c>. A document without inputs prints no line.</summary>
public static class InputsText
{
    public static string? Line(GraphDocument doc, EvalSnapshot? snapshot)
        => doc.Inputs.Count == 0
            ? null
            : "inputs " + string.Join(", ", doc.Inputs.OrderBy(i => i.Name, StringComparer.Ordinal).Select(i => Entry(i, snapshot))) + ";";

    private static string Entry(GraphInput input, EvalSnapshot? snapshot)
    {
        var text = $"{input.Name}: {input.Kind} = {Literals.QuoteLine(input.Default)}";
        return snapshot?.GraphInputs?.GetValueOrDefault(input.Name) is { } value && value != input.Default
            ? $"{text} now {Literals.QuoteLine(value)}"
            : text;
    }
}
