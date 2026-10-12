// Asks for a flow's name in a small form over the canvas host (TKT-253).
// Not window.prompt: embedded browsers throw on it. The answer is the name
// as a host analysis id (slugId), shown as the user types.

import { slugId } from "./ids.js";

export interface NamePromptOptions {
  readonly heading?: string;
  /** True when the id names a flow the user would overwrite. */
  readonly taken: (id: string) => boolean;
}

const STYLE_ID = "bof-app-name-styles";

function ensureStyles(doc: Document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = `
    .bof-app-name { position: absolute; inset: 0; z-index: 30; display: flex; align-items: center; justify-content: center;
      background: rgba(0, 0, 0, 0.25); font: 13px var(--bof-app-font); color: var(--bof-app-text); }
    .bof-app-name form { background: var(--bof-app-surface); border: 1px solid var(--bof-app-border); border-radius: 6px;
      padding: 16px; display: flex; flex-direction: column; gap: 8px; min-width: 280px; }
    .bof-app-name h2 { margin: 0; font-size: 15px; }
    .bof-app-name-note { color: var(--bof-app-dim); min-height: 1.3em; }
    .bof-app-name-buttons { display: flex; justify-content: flex-end; gap: 6px; }
  `;
  doc.head.appendChild(style);
}

/** Resolves with the chosen id, or null when the user cancels. */
export function askFlowName(host: HTMLElement, options: NamePromptOptions): Promise<string | null> {
  const doc = host.ownerDocument;
  ensureStyles(doc);
  const overlay = doc.createElement("div");
  overlay.className = "bof-app-name";
  const form = doc.createElement("form");
  const heading = doc.createElement("h2");
  heading.textContent = options.heading ?? "Name this flow";
  const input = doc.createElement("input");
  input.setAttribute("aria-label", "Flow name");
  input.placeholder = "door schedule per storey";
  const note = doc.createElement("div");
  note.className = "bof-app-name-note";
  const buttons = doc.createElement("div");
  buttons.className = "bof-app-name-buttons";
  const cancel = doc.createElement("button");
  cancel.type = "button";
  cancel.textContent = "Cancel";
  const ok = doc.createElement("button");
  ok.type = "submit";
  ok.textContent = "Save";
  buttons.append(cancel, ok);
  form.append(heading, input, note, buttons);
  overlay.appendChild(form);
  host.appendChild(overlay);

  /** The id the input names, or why it cannot be used. */
  const check = (): { id: string; problem: string | null } => {
    const id = slugId(input.value);
    if (!id) return { id, problem: "Type a name with a letter or a digit." };
    if (options.taken(id)) return { id, problem: `A flow named ${id} exists; choose another name.` };
    return { id, problem: null };
  };
  const show = () => {
    const { id, problem } = check();
    note.textContent = problem ?? `Saved as ${id}`;
    ok.disabled = problem !== null;
  };
  input.addEventListener("input", show);
  show();
  input.focus();

  return new Promise((resolve) => {
    const finish = (id: string | null) => {
      overlay.remove();
      resolve(id);
    };
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      const { id, problem } = check();
      if (problem === null) finish(id);
    });
    cancel.addEventListener("click", () => finish(null));
    overlay.addEventListener("keydown", (e) => {
      if (e.key === "Escape") { e.stopPropagation(); finish(null); }
    });
  });
}
