// The problems strip along the bottom of the canvas host: a one-line summary
// that expands to the list from graphProblems, root causes first. Hidden when
// the flow has no problems. Selecting is the caller's job (deps.onSelect).

import { problemsSummary, type Problem } from "./graphProblems";

const STYLE_ID = "bof-app-problems-styles";

export interface ProblemsPanelDeps {
  readonly onSelect: (nodeId: string) => void;
}

export interface ProblemsPanel {
  render(problems: readonly Problem[]): void;
  dispose(): void;
}

function ensureStyles(doc: Document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = `
    .bof-app-problems-strip { position: absolute; left: 0; right: 0; bottom: 0; z-index: 5; font-size: 12px;
      background: rgba(30, 30, 34, 0.94); color: #e8e8ea; border-top: 1px solid rgba(255, 255, 255, 0.18); }
    .bof-app-problems-summary { padding: 4px 10px; cursor: pointer; user-select: none; }
    .bof-app-problems-list { max-height: 30vh; overflow-y: auto; }
    .bof-app-problems-entry { display: flex; gap: 8px; padding: 3px 10px; cursor: pointer; border-top: 1px solid rgba(255, 255, 255, 0.08); }
    .bof-app-problems-entry:hover { background: rgba(255, 255, 255, 0.08); }
    .bof-app-problems-status { font-weight: 600; min-width: 90px; color: #ff8a80; }
    .bof-app-problems-Unavailable .bof-app-problems-status { color: #ffcc80; }
    .bof-app-problems-Unready .bof-app-problems-status { color: #ffe082; }
    .bof-app-problems-info .bof-app-problems-status { color: #90caf9; font-weight: normal; }
    .bof-app-problems-info { opacity: 0.8; }
    .bof-app-problems-text { opacity: 0.85; }
  `;
  doc.head.appendChild(style);
}

export function createProblemsPanel(host: HTMLElement, deps: ProblemsPanelDeps): ProblemsPanel {
  const doc = host.ownerDocument;
  ensureStyles(doc);

  const strip = doc.createElement("div");
  strip.className = "bof-app-problems-strip";
  strip.style.display = "none";
  const summary = doc.createElement("div");
  summary.className = "bof-app-problems-summary";
  const list = doc.createElement("div");
  list.className = "bof-app-problems-list";
  list.style.display = "none";
  strip.append(summary, list);
  host.appendChild(strip);

  summary.addEventListener("click", () => {
    list.style.display = list.style.display === "none" ? "" : "none";
  });

  const entry = (p: Problem): HTMLElement => {
    const row = doc.createElement("div");
    const info = p.status === "EffectPending" ? " bof-app-problems-info" : "";
    row.className = `bof-app-problems-entry bof-app-problems-${p.status}${info}`;
    row.dataset.nodeId = p.nodeId;
    const status = doc.createElement("span");
    status.className = "bof-app-problems-status";
    status.textContent = p.status;
    const name = doc.createElement("span");
    name.className = "bof-app-problems-node";
    name.textContent = `${p.title} (${p.nodeId})`;
    const text = doc.createElement("span");
    text.className = "bof-app-problems-text";
    text.textContent = p.text;
    row.append(status, name, text);
    row.addEventListener("click", () => deps.onSelect(p.nodeId));
    return row;
  };

  return {
    render(problems) {
      list.replaceChildren(...problems.map(entry));
      summary.textContent = problemsSummary(problems);
      strip.style.display = problems.length === 0 ? "none" : "";
    },
    dispose() {
      strip.remove();
    },
  };
}
