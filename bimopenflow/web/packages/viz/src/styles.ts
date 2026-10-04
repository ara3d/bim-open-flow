const STYLE_ID = "bof-viz-styles";

/** All styling under the bof-viz- prefix; theme via CSS custom properties. */
export const vizCss = `
.bof-viz-root {
  --bof-viz-font: 13px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif;
  --bof-viz-fg: #1f2430;
  --bof-viz-bg: #ffffff;
  --bof-viz-muted: #6b7280;
  --bof-viz-border: #d7dbe0;
  --bof-viz-header-bg: #f3f4f6;
  --bof-viz-row-alt-bg: #fafafa;
  --bof-viz-accent: #2563eb;
  --bof-viz-negative: #dc2626;
  --bof-viz-axis: #9ca3af;
  --bof-viz-series-0: #2563eb;
  --bof-viz-series-1: #d97706;
  --bof-viz-series-2: #059669;
  --bof-viz-series-3: #dc2626;
  --bof-viz-series-4: #7c3aed;
  --bof-viz-series-5: #0891b2;
  --bof-viz-series-6: #be185d;
  --bof-viz-series-7: #4d7c0f;
  font: var(--bof-viz-font);
  color: var(--bof-viz-fg);
  background: var(--bof-viz-bg);
}
.bof-viz-table { border-collapse: collapse; width: 100%; }
.bof-viz-table th, .bof-viz-table td {
  padding: 4px 8px;
  border-bottom: 1px solid var(--bof-viz-border);
  text-align: left;
  white-space: nowrap;
}
.bof-viz-table th {
  background: var(--bof-viz-header-bg);
  cursor: pointer;
  user-select: none;
  position: sticky;
  top: 0;
}
.bof-viz-num { text-align: right; font-variant-numeric: tabular-nums; }
.bof-viz-table th.bof-viz-num, .bof-viz-table td.bof-viz-num { text-align: right; }
.bof-viz-table tbody tr:nth-child(even) { background: var(--bof-viz-row-alt-bg); }
.bof-viz-footer { color: var(--bof-viz-muted); padding: 4px 8px; font-size: 12px; }
.bof-viz-bar { fill: var(--bof-viz-accent); }
.bof-viz-bar--neg { fill: var(--bof-viz-negative); }
.bof-viz-axis-line { stroke: var(--bof-viz-axis); stroke-width: 1; }
.bof-viz-tick { stroke: var(--bof-viz-axis); stroke-width: 1; }
.bof-viz-axis-label { fill: var(--bof-viz-fg); font-size: 11px; }
.bof-viz-tick-label { fill: var(--bof-viz-muted); font-size: 10px; }
.bof-viz-value-label { fill: var(--bof-viz-fg); font-size: 10px; }
.bof-viz-line { fill: none; stroke-width: 1.5; }
`;

/** Inject the stylesheet once per document. */
export const ensureStyles = (doc: Document): void => {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = vizCss;
  doc.head.appendChild(style);
};
