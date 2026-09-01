using System.Text.Json;
using System.Text.Json.Nodes;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>sink.exportJson: writes the input table as a JSON array of records
/// or as newline-delimited JSON (one object per line).</summary>
public sealed class ExportJsonNode : IFlowNode
{
    public const string Kind = "sink.exportJson";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("path", ParamKind.FilePath),
            new ParamSpec("layout", ParamKind.Enum, "records", new[] { "records", "lines" }),
            new ParamSpec("indent", ParamKind.Boolean, "false"),
        },
        "Writes the input table as JSON: 'records' is one array of objects (optionally indented), 'lines' is newline-delimited objects. Outputs a one-row summary (path, rowCount).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Kind);
        var table = inputs.TableAt(0);
        var path = parameters.RequiredPath("path");
        var layout = parameters.GetEnum("layout", Kind, "records", "records", "lines");
        var indent = parameters.GetBoolean("indent");
        if (indent && layout == "lines")
            throw new ArgumentException($"{Kind}: parameter 'indent' applies only to the 'records' layout");
        Sinks.ReplaceVia(path, temp =>
        {
            DuckWriting.CopyTable(table, temp, $"FORMAT JSON, ARRAY {(layout == "records" ? "true" : "false")}");
            if (indent)
                Reindent(temp);
        });
        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("exportJson",
                ("path", path),
                ("rowCount", (long)table.Rows.Count))),
        };
    }

    /// <summary>DuckDB cannot indent its JSON output, so re-serialize with System.Text.Json.</summary>
    private static void Reindent(string path)
        => File.WriteAllText(path,
            JsonNode.Parse(File.ReadAllText(path))!
                .ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
