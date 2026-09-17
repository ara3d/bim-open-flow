using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataFlowEngine.Runs;
using BimOpenFlow.Contracts;
using BimOpenFlow.Publishing;

namespace BimOpenFlow.Reports;

public sealed record ReportOptions(
    string Title,
    int MaxEvidenceRows = HtmlTables.DefaultMaxRows);

/// <summary>
/// Renders a run record as a static, archivable, printable HTML report with no
/// JavaScript: provenance header, verdict summary (when any recorded table
/// follows the verdict convention), capped evidence tables, and a node-output
/// hash appendix. Deterministic for a given (run, options).
/// </summary>
public static class ReportGenerator
{
    private const string PrintCss = """
@media print {
  body { max-width: none; padding: 0; }
  .bof-section { break-inside: avoid; }
  a { color: inherit; text-decoration: none; }
}
""";

    public static string FromRun(RunRecord run, ReportOptions options)
    {
        var builder = new HtmlDocumentBuilder(options.Title)
            .AddCss(PrintCss)
            .AddSection("bof-provenance", "Provenance", ProvenanceHtml(run));

        var verdicts = VerdictSummaries(run);
        if (verdicts.Count > 0)
            builder.AddSection("bof-verdicts", "Verdict summary", VerdictSummaryHtml(verdicts));

        builder.AddSection("bof-evidence", "Evidence", EvidenceHtml(run, options.MaxEvidenceRows));
        builder.AddSection("bof-node-outputs", "Appendix: node output hashes", NodeOutputsHtml(run));
        return builder.Build();
    }

    private static string ProvenanceHtml(RunRecord run)
    {
        var sb = new StringBuilder("<table class=\"bof-table\">\n<tbody>\n");
        Row(sb, "Graph hash", Hash(run.GraphHash));
        Row(sb, "Engine version", Html.Escape(run.EngineVersion));
        Row(sb, "Run timestamp (UTC)", Html.Escape(run.TimestampUtc));
        foreach (var input in RunRecorder.SortInputs(run.Inputs))
            Row(sb, $"Input {input.Node}.{input.Param}",
                Hash(input.ContentHash) + (input.Source is null ? "" : $" <span class=\"bof-muted\">({Html.Escape(input.Source)})</span>"));
        return sb.Append("</tbody>\n</table>").ToString();
    }

    private static IReadOnlyList<(string Port, VerdictCounts Counts)> VerdictSummaries(RunRecord run)
        => SortedTables(run)
            .Select(t => (t.Port, Counts: VerdictTables.TryCount(t.Table, out var c) ? c : null))
            .Where(t => t.Counts is not null)
            .Select(t => (t.Port, t.Counts!))
            .ToList();

    private static string VerdictSummaryHtml(IReadOnlyList<(string Port, VerdictCounts Counts)> summaries)
    {
        var sb = new StringBuilder("<table class=\"bof-table\">\n<thead>\n<tr>" +
            "<th>Output</th><th>Pass</th><th>Fail</th><th>Needs review</th>" +
            "<th>Info not available</th><th>Total</th><th>Worst</th></tr>\n</thead>\n<tbody>\n");
        foreach (var (port, c) in summaries)
        {
            sb.Append($"<tr><td>{Html.Escape(port)}</td>");
            Count(sb, c.Pass, Verdict.Pass);
            Count(sb, c.Fail, Verdict.Fail);
            Count(sb, c.NeedsReview, Verdict.NeedsReview);
            Count(sb, c.InfoNotAvailable, Verdict.InfoNotAvailable);
            sb.Append($"<td class=\"bof-num\">{c.Total}</td>");
            sb.Append($"<td class=\"{VerdictClass(c.Worst)}\">{c.Worst}</td></tr>\n");
        }
        return sb.Append("</tbody>\n</table>").ToString();
    }

    private static string EvidenceHtml(RunRecord run, int maxRows)
    {
        var sb = new StringBuilder();
        foreach (var (port, value) in SortedOutputs(run))
        {
            sb.Append($"<h3>{Html.Escape(port)}</h3>\n");
            sb.Append(value is TableValue t
                ? t.Table.ToHtml(maxRows)
                : $"<p>{Html.Escape(value.Kind.ToString())}: <code>{Html.Escape(ScalarText(value))}</code></p>\n");
        }
        return sb.ToString();
    }

    private static string NodeOutputsHtml(RunRecord run)
    {
        var sb = new StringBuilder("<table class=\"bof-table\">\n<thead>\n<tr>" +
            "<th>Output</th><th>SHA-256</th></tr>\n</thead>\n<tbody>\n");
        foreach (var (port, hash) in run.NodeOutputs.OrderBy(p => p.Key, StringComparer.Ordinal))
            sb.Append($"<tr><td>{Html.Escape(port)}</td><td>{Hash(hash)}</td></tr>\n");
        return sb.Append("</tbody>\n</table>").ToString();
    }

    private static IEnumerable<(string Port, FlowValue Value)> SortedOutputs(RunRecord run)
        => run.RecordedOutputs.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => (p.Key, p.Value));

    private static IEnumerable<(string Port, Ara3D.DataTable.IDataTable Table)> SortedTables(RunRecord run)
        => SortedOutputs(run)
            .Where(p => p.Value is TableValue)
            .Select(p => (p.Port, ((TableValue)p.Value).Table));

    private static string ScalarText(FlowValue value)
        => value switch
        {
            BooleanValue b => b.Value ? "true" : "false",
            IntegerValue i => i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            NumberValue n => HtmlTables.FormatCell(n.Value),
            TextValue t => t.Value,
            _ => throw new ArgumentException($"Unexpected scalar value {value.Kind}"),
        };

    private static string VerdictClass(Verdict verdict)
        => $"bof-verdict-{verdict.ToString().ToLowerInvariant()}";

    private static void Count(StringBuilder sb, int count, Verdict verdict)
        => sb.Append(count > 0
            ? $"<td class=\"bof-num {VerdictClass(verdict)}\">{count}</td>"
            : "<td class=\"bof-num\">0</td>");

    private static void Row(StringBuilder sb, string label, string valueHtml)
        => sb.Append($"<tr><th>{Html.Escape(label)}</th><td>{valueHtml}</td></tr>\n");

    private static string Hash(string hash)
        => $"<span class=\"bof-hash\">{Html.Escape(hash)}</span>";
}
