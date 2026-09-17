using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using BimOpenFlow.Publishing;

namespace BimOpenFlow.Dashboards;

/// <summary>
/// Generates a self-contained interactive HTML dashboard from a frozen run:
/// the viz bundle, the referenced recorded tables as embedded JSON, and an
/// init script mounting one viz component per spec item. Deterministic for a
/// given (run, spec, bundle).
/// TODO: live-session variant observing a running host over the analysisEvents
/// SSE channel; static-from-run is the v1 deliverable.
/// </summary>
public static class DashboardGenerator
{
    public const string DataGlobal = "bofDashboardData";

    public static string FromRun(RunRecord run, DashboardSpec spec, VizBundle bundle)
    {
        var builder = new HtmlDocumentBuilder(spec.Title)
            .AddBody(Header(run))
            .AddCss(".bof-widget { margin: 0.5rem 0; overflow-x: auto; }");

        for (var i = 0; i < spec.Items.Count; i++)
        {
            var item = spec.Items[i];
            builder.AddSection($"bof-widget-{i}", item.Title ?? item.OutputPort,
                $"<div class=\"bof-widget\" id=\"bof-mount-{i}\"></div>");
        }

        return builder
            .AddScript(bundle.Js)
            .AddScript(DataScript(run, spec))
            .AddScript(InitScript(spec))
            .Build();
    }

    private static string Header(RunRecord run)
        => $"<p class=\"bof-muted\">Run of graph <span class=\"bof-hash\">{Html.Escape(run.GraphHash)}</span>" +
           $" at {Html.Escape(run.TimestampUtc)}</p>";

    private static string DataScript(RunRecord run, DashboardSpec spec)
    {
        var ports = spec.Items.Select(i => i.OutputPort)
            .Distinct().OrderBy(p => p, StringComparer.Ordinal);
        var sb = new StringBuilder($"const {DataGlobal} = {{\n");
        foreach (var port in ports)
            sb.Append($"  {JsString(port)}: {TableFor(run, port).ToJson()},\n");
        return sb.Append("};").ToString();
    }

    private static string InitScript(DashboardSpec spec)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < spec.Items.Count; i++)
        {
            var item = spec.Items[i];
            sb.Append($"BofViz.{ComponentName(item.Widget)}.mount(")
                .Append($"document.getElementById(\"bof-mount-{i}\"), ")
                .Append($"{DataGlobal}[{JsString(item.OutputPort)}]")
                .Append(item.OptionsJson is null ? ");\n" : $", {item.OptionsJson});\n");
        }
        return sb.ToString();
    }

    private static Ara3D.DataTable.IDataTable TableFor(RunRecord run, string port)
        => run.RecordedOutputs.TryGetValue(port, out var value)
            ? value is TableValue t
                ? t.Table
                : throw new ArgumentException($"Recorded output '{port}' is a {value.Kind}, not a Table")
            : throw new ArgumentException($"Run has no recorded output '{port}'");

    private static string ComponentName(DashboardWidget widget)
        => widget switch
        {
            DashboardWidget.Table => "DataTableView",
            DashboardWidget.BarChart => "BarChart",
            DashboardWidget.LineChart => "LineChart",
            _ => throw new ArgumentOutOfRangeException(nameof(widget)),
        };

    private static string JsString(string text)
        => System.Text.Json.JsonSerializer.Serialize(text);
}
