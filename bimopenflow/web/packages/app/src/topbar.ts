// Top bar: analysis picker + new, save (with dirty indicator), run, canvas
// theme picker, and connection status.

import type { AnalysisSummary } from "@bimopenflow/contracts";
import {
  canvasThemeNames,
  isCanvasThemeName,
  type CanvasThemeName,
} from "./canvasTheme.js";

export type ConnectionStatus = "connected" | "offline" | "connecting";

export interface TopbarHandlers {
  onOpenAnalysis(id: string): void;
  onNewAnalysis(): void;
  onSave(): void;
  onRun(): void;
  onThemeChange(name: CanvasThemeName): void;
}

export interface Topbar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: ConnectionStatus): void;
  setTheme(name: CanvasThemeName): void;
}

export function createTopbar(root: HTMLElement, handlers: TopbarHandlers): Topbar {
  const doc = root.ownerDocument;
  root.classList.add("bof-app-topbar");

  const title = doc.createElement("strong");
  title.textContent = "BimOpenFlow";

  const picker = doc.createElement("select");
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

  const themePicker = doc.createElement("select");
  themePicker.title = "Canvas theme";
  for (const name of canvasThemeNames) {
    const opt = doc.createElement("option");
    opt.value = name;
    opt.textContent = name;
    themePicker.appendChild(opt);
  }
  themePicker.addEventListener("change", () => {
    if (isCanvasThemeName(themePicker.value))
      handlers.onThemeChange(themePicker.value);
  });

  const conn = doc.createElement("span");
  conn.className = "bof-app-conn";
  conn.textContent = "connecting…";

  root.append(title, picker, newBtn, saveBtn, dirtyMark, runBtn, themePicker, conn);

  return {
    setAnalyses(list, activeId) {
      picker.textContent = "";
      const placeholder = doc.createElement("option");
      placeholder.value = "";
      placeholder.textContent = list.length ? "— open analysis —" : "no analyses";
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
    setConnection(status) {
      conn.textContent = status;
      conn.classList.toggle("bof-app-conn-ok", status === "connected");
      conn.classList.toggle("bof-app-conn-bad", status === "offline");
    },
  };
}
