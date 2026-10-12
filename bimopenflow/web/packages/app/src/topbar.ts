// Top bar: analysis picker + new, save (with dirty indicator), run, canvas
// theme picker, and host connection status. The page-level
// host banner is mountHostBanner in @bimopenflow/client/host.

import type { FlowGroup } from "./flowList.js";
import {
  canvasThemeNames,
  isCanvasThemeName,
  type CanvasThemeName,
} from "@bimopenflow/graph";
import type { HostStatus } from "@bimopenflow/client/host";
import { brandMark } from "./brand.js";

export interface TopbarHandlers {
  /** Replaces the default "BIM Open Flow" heading. */
  heading?: string;
  onOpenAnalysis(id: string): void;
  onNewAnalysis(): void;
  onSave(): void;
  onRun(): void;
  onThemeChange(name: CanvasThemeName): void;
}

export interface Topbar {
  setAnalyses(groups: readonly FlowGroup[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: HostStatus): void;
  setTheme(name: CanvasThemeName): void;
}

/** A <select> over `names` that calls `onPick` with a validated choice. */
export function namePicker<T extends string>(
  doc: Document,
  label: string,
  names: readonly T[],
  isName: (value: string) => value is T,
  onPick: (name: T) => void,
): HTMLSelectElement {
  const select = doc.createElement("select");
  select.title = label;
  select.setAttribute("aria-label", label);
  for (const name of names) {
    const opt = doc.createElement("option");
    opt.value = name;
    opt.textContent = name;
    select.appendChild(opt);
  }
  select.addEventListener("change", () => {
    if (isName(select.value)) onPick(select.value);
  });
  return select;
}

/** Fills a flow picker with `groups`, one optgroup per labelled group,
 *  `activeId` selected, each option titled by `title` (its id by default).
 *  `leading` options (a placeholder) come first. */
export function fillFlowPicker(
  select: HTMLSelectElement,
  groups: readonly FlowGroup[],
  activeId: string | null,
  leading: readonly HTMLOptionElement[] = [],
  title: (id: string) => string = (id) => id,
): void {
  const doc = select.ownerDocument;
  const option = (id: string) => new Option(title(id), id, false, id === activeId);
  select.replaceChildren(...leading, ...groups.map((g) => {
    if (g.label === null) return g.flows.map((a) => option(a.id));
    const group = doc.createElement("optgroup");
    group.label = g.label;
    group.append(...g.flows.map((a) => option(a.id)));
    return [group];
  }).flat());
}

export function createTopbar(root: HTMLElement, handlers: TopbarHandlers): Topbar {
  const doc = root.ownerDocument;
  root.classList.add("bof-app-topbar");

  const title = doc.createElement("strong");
  title.className = "bof-app-brand";
  title.appendChild(brandMark(doc, "bof-app-brand-mark"));
  title.append(handlers.heading ?? "BIM Open Flow");

  const picker = doc.createElement("select");
  picker.setAttribute("aria-label", "Open flow");
  picker.addEventListener("change", () => {
    if (picker.value) handlers.onOpenAnalysis(picker.value);
  });

  const newBtn = doc.createElement("button");
  newBtn.textContent = "New";
  newBtn.addEventListener("click", handlers.onNewAnalysis);

  const saveBtn = doc.createElement("button");
  saveBtn.textContent = "Save";
  saveBtn.addEventListener("click", handlers.onSave);

  const dirtyMark = doc.createElement("span");
  dirtyMark.className = "bof-app-dirty";

  const runBtn = doc.createElement("button");
  runBtn.textContent = "Run";
  runBtn.addEventListener("click", handlers.onRun);

  const themePicker = namePicker(doc, "Canvas theme", canvasThemeNames, isCanvasThemeName, handlers.onThemeChange);

  const conn = doc.createElement("span");
  conn.className = "bof-app-conn";
  conn.textContent = "reconnecting…";

  root.append(title, picker, newBtn, saveBtn, dirtyMark, runBtn, themePicker, conn);

  return {
    setAnalyses(groups, activeId) {
      fillFlowPicker(picker, groups, activeId, [new Option(groups.length ? "— open flow —" : "no flows", "")]);
    },
    setDirty(dirty) {
      dirtyMark.textContent = dirty ? "● unsaved" : "";
      saveBtn.disabled = !dirty;
    },
    setTheme(name) {
      themePicker.value = name;
    },
    setConnection(status) {
      conn.textContent = status === "reconnecting" ? "reconnecting…" : status;
      conn.classList.toggle("bof-app-conn-ok", status === "connected");
      conn.classList.toggle("bof-app-conn-bad", status === "offline");
    },
  };
}
