// Top bar: analysis picker + new, save (with dirty indicator), run, and
// connection status.

import type { AnalysisSummary } from "@bimopenflow/contracts";

export type ConnectionStatus = "connected" | "offline" | "connecting";

export interface TopbarHandlers {
  onOpenAnalysis(id: string): void;
  onNewAnalysis(): void;
  onSave(): void;
  onRun(): void;
}

export interface Topbar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: ConnectionStatus): void;
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

  const conn = doc.createElement("span");
  conn.className = "bof-app-conn";
  conn.textContent = "connecting…";

  root.append(title, picker, newBtn, saveBtn, dirtyMark, runBtn, conn);

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
    setConnection(status) {
      conn.textContent = status;
      conn.classList.toggle("bof-app-conn-ok", status === "connected");
      conn.classList.toggle("bof-app-conn-bad", status === "offline");
    },
  };
}
