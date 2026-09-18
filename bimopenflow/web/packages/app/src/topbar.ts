// Top bar: analysis picker + new, save (with dirty indicator), run, canvas
// theme picker, and host connection status. Also the page-level host banner
// that every page mounts, inside or outside the shell.

import type { AnalysisSummary } from "@bimopenflow/contracts";
import {
  canvasThemeNames,
  isCanvasThemeName,
  type CanvasThemeName,
} from "./canvasTheme.js";
import { hostStatusMessage, type HostStatus, type HostStatusSource } from "./hostStatus.js";
import { ensureAppStyles } from "./styles.js";

export interface TopbarHandlers {
  /** Replaces the default "BimOpenFlow · Snowdon 3D graphs" heading. */
  heading?: string;
  onOpenAnalysis(id: string): void;
  onNewAnalysis(): void;
  onSave(): void;
  onRun(): void;
  onThemeChange(name: CanvasThemeName): void;
}

export interface Topbar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: HostStatus): void;
  setTheme(name: CanvasThemeName): void;
}

export function createTopbar(root: HTMLElement, handlers: TopbarHandlers): Topbar {
  const doc = root.ownerDocument;
  root.classList.add("bof-app-topbar");

  const title = doc.createElement("strong");
  if (handlers.heading) title.textContent = handlers.heading;
  else {
    title.textContent = "BimOpenFlow";
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
  conn.textContent = "reconnecting…";

  root.append(title, picker, newBtn, saveBtn, dirtyMark, runBtn, themePicker, conn);

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
    setConnection(status) {
      conn.textContent = status === "reconnecting" ? "reconnecting…" : status;
      conn.classList.toggle("bof-app-conn-ok", status === "connected");
      conn.classList.toggle("bof-app-conn-bad", status === "offline");
    },
  };
}

/**
 * A fixed strip along the bottom of the page that names the host API url and
 * how to recover whenever the host is not connected; hidden while it is.
 * Returns the unsubscribe.
 */
export function mountHostBanner(
  doc: Document,
  host: HostStatusSource,
  apiUrl = new URL("/api", doc.baseURI).href,
): () => void {
  ensureAppStyles(doc);
  const banner = doc.createElement("div");
  banner.className = "bof-app-host-banner";
  banner.setAttribute("role", "alert");
  const render = (status: HostStatus) => {
    banner.textContent = hostStatusMessage(status, apiUrl);
    banner.hidden = status === "connected";
    banner.classList.toggle("bof-app-host-banner-offline", status === "offline");
  };
  render(host.get().status);
  doc.body.appendChild(banner);
  const unsubscribe = host.subscribe((state) => render(state.status));
  return () => {
    unsubscribe();
    banner.remove();
  };
}
