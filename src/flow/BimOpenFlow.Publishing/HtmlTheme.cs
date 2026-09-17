namespace BimOpenFlow.Publishing;

/// <summary>
/// The default inline CSS theme for published documents: CSS custom properties
/// under the bof- prefix (consistent with the viz package's --bof-viz-*),
/// light by default. Hosts restyle by overriding the variables.
/// </summary>
public static class HtmlTheme
{
    public const string Default = """
:root {
  --bof-bg: #ffffff;
  --bof-fg: #1c1e26;
  --bof-muted: #6a6f7e;
  --bof-border: #d9dbe3;
  --bof-surface: #f5f6f9;
  --bof-accent: #2b6cb0;
  --bof-pass: #2f855a;
  --bof-fail: #c53030;
  --bof-needs-review: #b7791f;
  --bof-info-not-available: #718096;
  --bof-font: system-ui, -apple-system, "Segoe UI", sans-serif;
  --bof-mono: ui-monospace, Consolas, "Courier New", monospace;
}
body {
  margin: 0 auto;
  max-width: 60rem;
  padding: 1.5rem;
  background: var(--bof-bg);
  color: var(--bof-fg);
  font-family: var(--bof-font);
  line-height: 1.5;
}
h1, h2, h3 { line-height: 1.2; }
code, .bof-hash { font-family: var(--bof-mono); font-size: 0.875em; }
.bof-section { margin: 2rem 0; }
.bof-muted { color: var(--bof-muted); }
.bof-table { border-collapse: collapse; width: 100%; font-size: 0.875rem; }
.bof-table th, .bof-table td {
  border: 1px solid var(--bof-border);
  padding: 0.25rem 0.5rem;
  text-align: left;
  vertical-align: top;
}
.bof-table th { background: var(--bof-surface); }
.bof-table td.bof-num { text-align: right; font-variant-numeric: tabular-nums; }
.bof-table-note { color: var(--bof-muted); font-size: 0.8rem; margin: 0.25rem 0 0; }
.bof-verdict-pass { color: var(--bof-pass); }
.bof-verdict-fail { color: var(--bof-fail); }
.bof-verdict-needsreview { color: var(--bof-needs-review); }
.bof-verdict-infonotavailable { color: var(--bof-info-not-available); }
""";
}
