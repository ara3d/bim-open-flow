// App-shell styling: one injected stylesheet under the bof-app- prefix,
// themed by CSS custom properties (same approach as viz/panes).

export const appCss = `
:root {
  --bof-app-bg: #f7f6f3;
  --bof-app-surface: #ffffff;
  --bof-app-border: #e5e3de;
  --bof-app-text: #1a1a18;
  --bof-app-dim: #8a8880;
  --bof-app-accent: #3b82c4;
  --bof-app-green: #3ba55d;
  --bof-app-amber: #d99a2b;
  --bof-app-red: #c0392b;
  --bof-app-hover: #ecebe4;
  --bof-app-font: Inter, "Segoe UI", system-ui, sans-serif;
  --bof-app-left: 240px;
  --bof-app-right: 420px;
}
html, body { margin: 0; height: 100%; }
.bof-app-root {
  display: grid;
  grid-template-rows: 40px minmax(0, 1fr);
  height: 100vh;
  background: var(--bof-app-bg);
  color: var(--bof-app-text);
  font: 13px var(--bof-app-font);
}
.bof-app-main {
  display: grid;
  grid-template-columns: var(--bof-app-left) 6px minmax(0, 1fr) 6px var(--bof-app-right);
  min-height: 0;
}
.bof-app-topbar {
  display: flex; align-items: center; gap: 8px; padding: 0 10px;
  background: var(--bof-app-surface); border-bottom: 1px solid var(--bof-app-border);
}
.bof-app-topbar select, .bof-app-topbar button, .bof-app-sidebar input {
  font: inherit; color: inherit;
  background: var(--bof-app-bg); border: 1px solid var(--bof-app-border);
  border-radius: 4px; padding: 3px 8px;
}
.bof-app-topbar button:hover { background: var(--bof-app-hover); cursor: pointer; }
.bof-app-topbar button:disabled { color: var(--bof-app-dim); cursor: default; }
.bof-app-dirty { color: var(--bof-app-amber); font-weight: 600; }
.bof-app-conn { margin-left: auto; color: var(--bof-app-dim); }
.bof-app-conn.bof-app-conn-ok { color: var(--bof-app-green); }
.bof-app-conn.bof-app-conn-bad { color: var(--bof-app-red); }
.bof-app-sidebar {
  display: flex; flex-direction: column; min-height: 0; overflow: hidden;
  background: var(--bof-app-surface); border-right: 1px solid var(--bof-app-border);
  padding: 8px; gap: 8px;
}
.bof-app-sidebar h3 { margin: 4px 0; font-size: 11px; text-transform: uppercase; color: var(--bof-app-dim); flex: none; }
.bof-app-sidebar input { width: 100%; box-sizing: border-box; flex: none; }
.bof-app-list { display: flex; flex-direction: column; gap: 2px; }
.bof-app-analyses { flex: 0 1 auto; max-height: 35%; overflow-y: auto; }
.bof-app-catalog { flex: 1 1 0; min-height: 0; overflow-y: auto; }
.bof-app-item {
  padding: 4px 6px; border-radius: 4px; cursor: pointer; border: 1px solid transparent;
}
.bof-app-item:hover { background: var(--bof-app-hover); }
.bof-app-item.bof-app-item-active { border-color: var(--bof-app-accent); }
.bof-app-item small { display: block; color: var(--bof-app-dim); }
.bof-app-catalog-group {
  margin: 8px 0 2px; font-size: 10px; text-transform: uppercase;
  letter-spacing: 0.06em; color: var(--bof-app-dim); flex: none;
}
.bof-app-canvas-host { position: relative; min-width: 0; }
.bof-app-canvas-host canvas { display: block; width: 100%; height: 100%; }
.bof-app-splitter {
  cursor: col-resize; background: var(--bof-app-bg);
  border-left: 1px solid var(--bof-app-border); border-right: 1px solid var(--bof-app-border);
}
.bof-app-splitter:hover { background: var(--bof-app-hover); }
.bof-app-split-ghost {
  position: fixed; top: 0; bottom: 0; width: 2px;
  background: var(--bof-app-accent); z-index: 50; pointer-events: none;
}
.bof-app-panearea {
  display: flex; flex-direction: column; min-height: 0; min-width: 0;
  background: var(--bof-app-surface);
}
.bof-app-tabs {
  display: flex; gap: 2px; padding: 4px 6px 0;
  border-bottom: 1px solid var(--bof-app-border); flex: none;
}
.bof-app-tab {
  padding: 4px 10px; border: 1px solid var(--bof-app-border); border-bottom: none;
  border-radius: 5px 5px 0 0; cursor: pointer; background: var(--bof-app-bg);
  color: var(--bof-app-dim);
}
.bof-app-tab.bof-app-tab-active { background: var(--bof-app-surface); color: var(--bof-app-text); }
.bof-app-panebody { flex: 1; overflow: auto; min-height: 0; padding: 6px; }
.bof-app-empty { color: var(--bof-app-dim); padding: 12px; }
.bof-app-params { display: grid; grid-template-columns: max-content 1fr; gap: 6px 10px; align-items: center; }
.bof-app-params label { color: var(--bof-app-dim); }
.bof-app-params input, .bof-app-params select {
  font: inherit; border: 1px solid var(--bof-app-border); border-radius: 4px; padding: 3px 6px;
  background: var(--bof-app-bg); color: inherit; width: 100%; box-sizing: border-box;
}
.bof-app-toasts { position: fixed; right: 12px; bottom: 12px; display: flex; flex-direction: column; gap: 6px; z-index: 100; }
.bof-app-toast {
  background: var(--bof-app-text); color: var(--bof-app-surface);
  padding: 8px 12px; border-radius: 6px; max-width: 360px; box-shadow: 0 2px 10px rgba(0,0,0,.25);
}
.bof-app-toast.bof-app-toast-error { background: var(--bof-app-red); }
`;

const STYLE_ID = "bof-app-styles";

/** Injects the app stylesheet once per document. */
export function ensureAppStyles(doc: Document = document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = appCss;
  doc.head.appendChild(style);
}
