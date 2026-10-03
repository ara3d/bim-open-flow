// Top bar: analysis picker + new, save (with dirty indicator), run, canvas
// theme picker, node style picker, and host connection status. The page-level
// host banner is mountHostBanner in @bimopenflow/client/host.

import type { AnalysisSummary } from "@bimopenflow/contracts";
import {
  canvasThemeNames,
  isCanvasThemeName,
  type CanvasThemeName,
} from "@bimopenflow/graph";
import { isNodeStyleName, nodeStyleNames, type NodeStyleName } from "@bimopenflow/graph";
import type { HostStatus } from "@bimopenflow/client/host";
import { brandMark } from "./brand.js";

export interface TopbarHandlers {
  /** Replaces the default "BIM Open Flow · Snowdon 3D graphs" heading. */
  heading?: string;
  onOpenAnalysis(id: string): void;
  onNewAnalysis(): void;
  onSave(): void;
  onRun(): void;
  onThemeChange(name: CanvasThemeName): void;
  /** Called with the node card style the user picked (TKT-98). The "Node
   *  style" picker is shown only when a page passes this handler. */
  onNodeStyleChange?(name: NodeStyleName): void;
}

export interface Topbar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: HostStatus): void;
  setTheme(name: CanvasThemeName): void;
  /** Shows `name` in the node style picker without calling the handler. */
  setNodeStyle(name: NodeStyleName): void;
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

export function createTopbar(root: HTMLElement, handlers: TopbarHandlers): Topbar {
  const doc = root.ownerDocument;
  root.classList.add("bof-app-topbar");

  const title = doc.createElement("strong");
  title.className = "bof-app-brand";
  title.appendChild(brandMark(doc, "bof-app-brand-mark"));
  if (handlers.heading) title.append(handlers.heading);
  else {
    title.append("BIM Open Flow");
    const lab = doc.createElement("a");
    lab.href = "/3d.html";
    lab.textContent = "Snowdon 3D graphs";
    title.append(" · ", lab);
  }

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
  const onNodeStyleChange = handlers.onNodeStyleChange;
  const stylePicker = onNodeStyleChange
    ? namePicker(doc, "Node style", nodeStyleNames, isNodeStyleName, onNodeStyleChange)
    : null;

  const conn = doc.createElement("span");
  conn.className = "bof-app-conn";
  conn.textContent = "reconnecting…";

  root.append(title, picker, newBtn, saveBtn, dirtyMark, runBtn, themePicker, ...(stylePicker ? [stylePicker] : []), conn);

  return {
    setAnalyses(list, activeId) {
      picker.textContent = "";
      const placeholder = doc.createElement("option");
      placeholder.value = "";
      placeholder.textContent = list.length ? "— open flow —" : "no flows";
      picker.appendChild(placeholder);
      for (const a of list) {
        const opt = doc.createElement("option");
        opt.value = a.id;
        opt.textContent = a.id;
        opt.selected = a.id === activeId;
        picker.appendChild(opt);
      }
    },
    setDirty(dirty) {
      dirtyMark.textContent = dirty ? "● unsaved" : "";
      saveBtn.disabled = !dirty;
    },
    setTheme(name) {
      themePicker.value = name;
    },
    setNodeStyle(name) {
      if (stylePicker) stylePicker.value = name;
    },
    setConnection(status) {
      conn.textContent = status === "reconnecting" ? "reconnecting…" : status;
      conn.classList.toggle("bof-app-conn-ok", status === "connected");
      conn.classList.toggle("bof-app-conn-bad", status === "offline");
    },
  };
}
