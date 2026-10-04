// The start page: an overlay over the canvas host listing the flows the
// sample folders define, grouped by folder, so a newcomer starts from one
// instead of a blank canvas (PROJECT.md workflow 1, principle 9). It only
// calls back; opening, copying, and creating a flow belong to app.ts.
import { groupTemplates, type FlowTemplate, type PresentTemplate } from "./templates.js";

export interface StartPageDeps {
  readonly templates: readonly FlowTemplate[];
  readonly onOpen: (id: string) => void;
  readonly onCopy: (id: string) => void;
  readonly onBlank: () => void;
}

export interface StartPage {
  /** Re-renders with the ids the host currently lists. */
  setPresent(ids: readonly string[]): void;
  show(): void;
  hide(): void;
  isOpen(): boolean;
  dispose(): void;
}

const STYLE_ID = "bof-app-start-styles";
export const ABSENT_NOTE = "not seeded in this host profile";

function ensureStartStyles(doc: Document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = `
    .bof-app-start { position: absolute; inset: 0; z-index: 20; overflow: auto; padding: 24px 32px;
      background: var(--bof-app-bg); color: var(--bof-app-text); font: 13px var(--bof-app-font); }
    .bof-app-start[hidden] { display: none; }
    .bof-app-start-head { display: flex; justify-content: space-between; align-items: center; }
    .bof-app-start-head h2 { margin: 0; font: 600 18px/1.3 var(--bof-app-font-brand, var(--bof-app-font)); }
    .bof-app-start-close { cursor: pointer; }
    .bof-app-start-group h3 { margin: 20px 0 8px; font-size: 13px; opacity: 0.8; }
    .bof-app-start-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 10px; }
    .bof-app-start-card { border: 1px solid var(--bof-app-border); border-radius: 6px; padding: 10px; background: var(--bof-app-surface);
      cursor: pointer; display: flex; flex-direction: column; gap: 6px; }
    .bof-app-start-card:hover, .bof-app-start-card:focus { border-color: var(--bof-app-accent); outline: none; }
    .bof-app-start-card-absent { opacity: 0.45; cursor: default; }
    .bof-app-start-card-absent:hover { border-color: var(--bof-app-border); }
    .bof-app-start-title { font-weight: 600; }
    .bof-app-start-desc, .bof-app-start-note, .bof-app-start-count { color: var(--bof-app-dim); }
    .bof-app-start-note { font-style: italic; }
    .bof-app-start-kinds { display: flex; flex-wrap: wrap; gap: 4px; }
    .bof-app-start-kind { font-size: 11px; padding: 1px 5px; border-radius: 3px; background: var(--bof-app-hover); }
    .bof-app-start-foot { display: flex; justify-content: space-between; align-items: center; }
  `;
  doc.head.appendChild(style);
}

export function createStartPage(host: HTMLElement, deps: StartPageDeps): StartPage {
  const doc = host.ownerDocument;
  ensureStartStyles(doc);
  const root = doc.createElement("div");
  root.className = "bof-app-start";
  root.hidden = true;
  host.appendChild(root);

  const el = <K extends keyof HTMLElementTagNameMap>(tag: K, cls: string, text?: string) => {
    const e = doc.createElement(tag);
    e.className = cls;
    if (text !== undefined) e.textContent = text;
    return e;
  };

  /** A clickable card: a click or Enter calls act. */
  const actionCard = (act: () => void): HTMLElement => {
    const card = el("div", "bof-app-start-card");
    card.tabIndex = 0;
    card.setAttribute("role", "button");
    card.addEventListener("click", act);
    card.addEventListener("keydown", (e) => { if (e.key === "Enter") act(); });
    return card;
  };

  const templateCard = (t: PresentTemplate): HTMLElement => {
    const card = t.present ? actionCard(() => deps.onOpen(t.id)) : el("div", "bof-app-start-card bof-app-start-card-absent");
    card.dataset.id = t.id;
    if (!t.present) card.setAttribute("aria-disabled", "true");
    card.appendChild(el("div", "bof-app-start-title", t.title));
    if (t.description) card.appendChild(el("div", "bof-app-start-desc", t.description));
    const kinds = el("div", "bof-app-start-kinds");
    for (const k of t.kinds) kinds.appendChild(el("span", "bof-app-start-kind", k));
    card.appendChild(kinds);
    const foot = el("div", "bof-app-start-foot");
    foot.appendChild(el("span", "bof-app-start-count", `${t.nodeCount} ${t.nodeCount === 1 ? "node" : "nodes"}`));
    if (t.present) {
      const copy = el("button", "bof-app-start-copy", "Copy");
      copy.type = "button";
      copy.title = "Open a copy of this flow under a new name";
      copy.addEventListener("click", (e) => { e.stopPropagation(); deps.onCopy(t.id); });
      foot.appendChild(copy);
    } else {
      foot.appendChild(el("span", "bof-app-start-note", ABSENT_NOTE));
    }
    card.appendChild(foot);
    return card;
  };

  const render = (present: ReadonlySet<string>): void => {
    root.replaceChildren();
    const head = el("div", "bof-app-start-head");
    head.appendChild(el("h2", "bof-app-start-heading", "Start from a flow"));
    const close = el("button", "bof-app-start-close", "Close");
    close.type = "button";
    close.addEventListener("click", () => hide());
    head.appendChild(close);
    root.appendChild(head);

    const blank = actionCard(() => deps.onBlank());
    blank.classList.add("bof-app-start-blank");
    blank.appendChild(el("div", "bof-app-start-title", "Blank flow"));
    blank.appendChild(el("div", "bof-app-start-desc", "An empty canvas; add nodes from the catalog."));
    const blankGrid = el("div", "bof-app-start-grid");
    blankGrid.appendChild(blank);
    root.appendChild(blankGrid);

    for (const g of groupTemplates(deps.templates, present)) {
      const section = el("section", "bof-app-start-group");
      section.dataset.folder = g.folder;
      section.appendChild(el("h3", "bof-app-start-group-label", g.label));
      const grid = el("div", "bof-app-start-grid");
      for (const t of g.templates) grid.appendChild(templateCard(t));
      section.appendChild(grid);
      root.appendChild(section);
    }
  };

  const onKey = (e: KeyboardEvent): void => {
    if (e.key === "Escape" && !root.hidden) hide();
  };
  doc.addEventListener("keydown", onKey);

  function hide(): void {
    root.hidden = true;
  }

  render(new Set());
  return {
    setPresent: (ids) => render(new Set(ids)),
    show: () => { root.hidden = false; },
    hide,
    isOpen: () => !root.hidden,
    dispose: () => {
      doc.removeEventListener("keydown", onKey);
      root.remove();
    },
  };
}
