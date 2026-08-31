const STYLE_ID = "bof-panes-styles";

/** All styling under the bof-panes- prefix; theme via CSS custom properties. */
export const panesCss = `
.bof-panes-root {
  --bof-panes-font: 13px/1.4 system-ui, -apple-system, "Segoe UI", sans-serif;
  --bof-panes-fg: #1f2430;
  --bof-panes-bg: #ffffff;
  --bof-panes-muted: #6b7280;
  --bof-panes-border: #d7dbe0;
  --bof-panes-selected-bg: #dbeafe;
  --bof-panes-pass: #059669;
  --bof-panes-fail: #dc2626;
  --bof-panes-review: #d97706;
  --bof-panes-noinfo: #6b7280;
  font: var(--bof-panes-font);
  color: var(--bof-panes-fg);
  background: var(--bof-panes-bg);
}
.bof-panes-root .bof-viz-table tbody tr.bof-panes-selected {
  background: var(--bof-panes-selected-bg);
}
.bof-panes-root .bof-viz-table tbody tr { cursor: pointer; }
.bof-panes-canvas { display: block; width: 100%; height: 100%; }
.bof-panes-title { font-weight: 600; margin: 4px 0; }
.bof-panes-section { color: var(--bof-panes-muted); margin: 8px 0 2px; font-size: 11px; text-transform: uppercase; }
.bof-panes-dl { display: grid; grid-template-columns: max-content 1fr; gap: 2px 12px; margin: 4px 0; }
.bof-panes-dl dt { color: var(--bof-panes-muted); }
.bof-panes-dl dd { margin: 0; }
.bof-panes-error { color: var(--bof-panes-fail); }
.bof-panes-warning { color: var(--bof-panes-review); }
.bof-panes-check {
  border: 1px solid var(--bof-panes-border);
  border-left: 3px solid var(--bof-panes-muted);
  padding: 4px 8px;
  margin: 4px 0;
  cursor: pointer;
}
.bof-panes-check--Pass { border-left-color: var(--bof-panes-pass); }
.bof-panes-check--Fail { border-left-color: var(--bof-panes-fail); }
.bof-panes-check--NeedsReview { border-left-color: var(--bof-panes-review); }
.bof-panes-check--InfoNotAvailable { border-left-color: var(--bof-panes-noinfo); }
.bof-panes-check--selected { background: var(--bof-panes-selected-bg); }
.bof-panes-check-title { font-weight: 600; }
.bof-panes-check-citation { color: var(--bof-panes-muted); font-size: 12px; }
.bof-panes-chip {
  display: inline-block;
  border-radius: 8px;
  padding: 0 6px;
  margin-right: 4px;
  font-size: 11px;
  color: #ffffff;
}
.bof-panes-chip--Pass { background: var(--bof-panes-pass); }
.bof-panes-chip--Fail { background: var(--bof-panes-fail); }
.bof-panes-chip--NeedsReview { background: var(--bof-panes-review); }
.bof-panes-chip--InfoNotAvailable { background: var(--bof-panes-noinfo); }
`;

/** Injects the stylesheet once per document (same approach as @bimopenflow/viz). */
export const ensurePaneStyles = (doc: Document): void => {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = panesCss;
  doc.head.appendChild(style);
};
