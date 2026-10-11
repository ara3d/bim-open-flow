// The FilePath control: a text box that offers the files under the host's model
// roots (the FilesUnderRoots suggestion), accepts any typed path, shows the path
// relative to the model root it sits under, flags a path that does not exist with
// a warning badge (never an error: people wire graphs before the data exists), and
// opens a host-served folder browser from a "..." button. One element per
// (node, param) row, held by the row's CanvasInstance like ColumnSelects.

import type { FolderEntry, FolderListing, PathStatus, SuggestionList } from "@bimopenflow/contracts";
import { canvasThemes, currentCanvasTheme } from "./canvasTheme.js";
import { styleIsland } from "./slotShared.js";
import { attachSuggestions } from "./suggestInput.js";
import { themeVersion } from "gratify";
import type { SourceMatch } from "./sources.js";

/** The host calls the control needs; the app binds them to the api client. */
export interface FileProvider {
  browse(dir: string, ext: string): Promise<FolderListing>;
  stat(path: string): Promise<PathStatus>;
}

export interface FilePathOptions {
  readonly readOnly?: boolean;
  readonly document?: Document;
  /** Null when the host offers no file endpoints (a static site): no browse button, no existence badge. */
  readonly files?: () => FileProvider | null;
  /** The source that already holds `value` for this row, if any (TKT-138): the row offers "use <source>". */
  readonly match?: (node: string, param: string, value: string) => SourceMatch | null;
  /** The user accepted the offer: link the row to `source`. The stored string stays. */
  readonly use?: (node: string, param: string, source: SourceMatch) => void;
}

/** The folder a browse starts in: the file's own folder when the path sits under a model root, else the roots list. */
export function startFolder(value: string, status: PathStatus | null): string {
  if (!status?.root) return "";
  const slash = value.replace(/\\/g, "/").lastIndexOf("/");
  return slash > 0 ? value.slice(0, slash) : status.root;
}

/** What the box shows when it is not being edited: the root-relative path when known, else the value. */
export function shownPath(value: string, status: PathStatus | null): string {
  return status?.relative ?? value;
}

/** True when the box should carry the not-found badge. An empty value is not a missing file. */
export function isMissing(value: string, status: PathStatus | null): boolean {
  return value.trim() !== "" && status !== null && !status.exists;
}

const WARN = "#d99a00";

interface Entry {
  el: HTMLDivElement;
  input: HTMLInputElement;
  badge: HTMLSpanElement;
  button: HTMLButtonElement;
  useButton: HTMLButtonElement;
  value: string;
  ext: string;
  status: PathStatus | null;
  themeV: number;
  ticket: number;
  closeBrowser: () => void;
  detach: () => void;
  render(): void;
  /** Shows or hides "use <source>" for the text now in the box. */
  renderMatch(): void;
  refresh(): Promise<void>;
}

export class FilePathControls {
  private entries = new Map<string, Entry>();
  private readonly readOnly: boolean;
  private readonly doc: Document;
  private readonly files: () => FileProvider | null;
  private readonly match: NonNullable<FilePathOptions["match"]>;
  private readonly use: NonNullable<FilePathOptions["use"]>;

  constructor(
    private fetchSuggestions: (node: string, param: string) => Promise<SuggestionList>,
    private commit: (node: string, param: string, value: string) => void,
    options: FilePathOptions = {},
  ) {
    this.readOnly = options.readOnly ?? false;
    this.doc = options.document ?? document;
    this.files = options.files ?? (() => null);
    this.match = options.match ?? (() => null);
    this.use = options.use ?? (() => {});
  }

  /** The row's element, created on first use and updated to `value` on later calls. */
  get(node: string, param: string, value: string, ext: string): HTMLDivElement {
    const key = `${node}::${param}`;
    let entry = this.entries.get(key);
    if (!entry) {
      entry = this.create(key, node, param, value, ext);
      this.entries.set(key, entry);
      void entry.refresh();
    }
    if (entry.value !== value && this.doc.activeElement !== entry.input) {
      entry.value = value;
      entry.status = null;
      entry.render();
      void entry.refresh();
    }
    entry.renderMatch();
    if (entry.themeV !== themeVersion) {
      entry.themeV = themeVersion;
      styleIsland(entry.input, canvasThemes[currentCanvasTheme()].palette);
      entry.render();
    }
    return entry.el;
  }

  /** Re-checks every path (a file may have appeared since). */
  refresh(): void {
    for (const entry of this.entries.values()) void entry.refresh();
  }

  prune(keys: ReadonlySet<string>): void {
    for (const [key, entry] of this.entries) {
      if (keys.has(key)) continue;
      entry.ticket++;
      entry.closeBrowser();
      entry.detach();
      entry.el.remove();
      this.entries.delete(key);
    }
  }

  private create(key: string, node: string, param: string, value: string, ext: string): Entry {
    const doc = this.doc;
    const el = doc.createElement("div");
    el.style.cssText = "display:flex;gap:4px;align-items:center;width:100%;height:100%";
    const input = doc.createElement("input");
    input.type = "text";
    input.spellcheck = false;
    input.disabled = this.readOnly;
    input.setAttribute("aria-label", `${node} ${param}`);
    input.style.flex = "1";
    input.style.minWidth = "0";
    const badge = doc.createElement("span");
    badge.textContent = "⚠";
    badge.setAttribute("role", "img");
    badge.setAttribute("aria-label", "File not found");
    badge.style.cssText = `color:${WARN};font:14px system-ui;flex-shrink:0`;
    badge.hidden = true;
    const button = doc.createElement("button");
    button.type = "button";
    button.textContent = "...";
    button.title = "Browse the model folders";
    button.setAttribute("aria-label", `${node} ${param} browse`);
    button.style.cssText = "flex-shrink:0;width:28px;height:100%;border:1px solid #aaa;border-radius:5px;" +
      "font:inherit;cursor:pointer;padding:0";
    button.disabled = this.readOnly;
    button.hidden = this.files() === null;
    const useButton = doc.createElement("button");
    useButton.type = "button";
    useButton.hidden = true;
    useButton.style.cssText = "flex-shrink:0;max-width:40%;height:100%;border:1px solid #aaa;border-radius:5px;" +
      "font:inherit;cursor:pointer;padding:0 6px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap";
    useButton.disabled = this.readOnly;
    el.append(input, badge, button, useButton);

    const entry: Entry = {
      el, input, badge, button, useButton, value, ext, status: null, themeV: -1, ticket: 0,
      closeBrowser: () => {}, detach: () => {}, render: () => {}, renderMatch: () => {}, refresh: async () => {},
    };
    entry.render = () => {
      const editing = doc.activeElement === input;
      input.value = editing ? entry.value : shownPath(entry.value, entry.status);
      input.title = entry.value;
      const missing = isMissing(entry.value, entry.status);
      badge.hidden = !missing;
      badge.title = missing ? `File not found: ${entry.value}` : "";
      input.style.borderColor = missing ? WARN : "";
      button.hidden = this.files() === null;
      entry.renderMatch();
    };
    let offered: SourceMatch | null = null;
    entry.renderMatch = () => {
      offered = this.readOnly ? null : this.match(node, param, doc.activeElement === input ? input.value : entry.value);
      useButton.hidden = offered === null;
      useButton.textContent = offered ? `use ${offered.id}` : "";
      useButton.title = offered ? `Link to ${offered.id}, which holds this path` : "";
      useButton.setAttribute("aria-label", `${node} ${param} use source`);
    };
    useButton.addEventListener("click", () => { if (offered) this.use(node, param, offered); });
    input.addEventListener("input", () => entry.renderMatch());
    entry.refresh = async () => {
      const ticket = ++entry.ticket;
      const files = this.files();
      if (!files || entry.value.trim() === "") {
        entry.status = null;
        entry.render();
        return;
      }
      try {
        const status = await files.stat(entry.value);
        if (ticket !== entry.ticket) return;
        entry.status = status;
      } catch {
        if (ticket !== entry.ticket) return;
        entry.status = null; // an unreachable host says nothing about the file
      }
      entry.render();
    };
    const set = (path: string) => {
      if (path === entry.value) return entry.render();
      entry.value = path;
      entry.status = null;
      entry.render();
      this.commit(node, param, path);
      void entry.refresh();
    };

    input.addEventListener("focus", () => { input.value = entry.value; });
    input.addEventListener("blur", () => entry.render());
    input.addEventListener("change", () => set(input.value.trim()));
    input.addEventListener("keydown", (e) => {
      if (e.key === "Enter") input.blur();
      if (e.key === "Escape") { input.value = entry.value; input.blur(); e.stopPropagation(); }
    });
    entry.detach = attachSuggestions(input, `bof-files-${key}`, () => this.fetchSuggestions(node, param),
      { picker: { multi: false } });
    button.addEventListener("click", () => {
      entry.closeBrowser();
      const files = this.files();
      if (files) entry.closeBrowser = this.openBrowser(entry, files, set);
    });
    return entry;
  }

  /** The folder browser popup. Returns its closer. */
  private openBrowser(entry: Entry, files: FileProvider, pick: (path: string) => void): () => void {
    const doc = this.doc;
    const popup = doc.createElement("div");
    popup.setAttribute("role", "dialog");
    popup.setAttribute("aria-label", "Browse model folders");
    popup.style.cssText =
      "position:fixed;z-index:10001;width:360px;max-height:320px;overflow:auto;background:#fff;color:#222;" +
      "border:1px solid #aaa;border-radius:4px;font:13px system-ui;box-shadow:0 4px 12px #0004";
    const r = entry.el.getBoundingClientRect();
    popup.style.left = `${r.left}px`;
    popup.style.top = `${r.bottom + 2}px`;
    doc.body.appendChild(popup);

    let closed = false;
    const close = () => {
      if (closed) return;
      closed = true;
      doc.removeEventListener("mousedown", outside, true);
      doc.removeEventListener("keydown", onKey, true);
      popup.remove();
    };
    const outside = (e: Event) => { if (!popup.contains(e.target as Node) && e.target !== entry.button) close(); };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") { e.stopPropagation(); close(); } };
    doc.addEventListener("mousedown", outside, true);
    doc.addEventListener("keydown", onKey, true);

    const row = (text: string, detail: string, onClick: () => void): HTMLDivElement => {
      const el = doc.createElement("div");
      el.style.cssText = "display:flex;justify-content:space-between;gap:12px;padding:3px 8px;cursor:pointer";
      const name = doc.createElement("span");
      name.textContent = text;
      const more = doc.createElement("span");
      more.textContent = detail;
      more.style.opacity = "0.6";
      el.append(name, more);
      el.addEventListener("click", onClick);
      return el;
    };
    const show = async (dir: string) => {
      let listing: FolderListing;
      try {
        listing = await files.browse(dir, entry.ext);
      } catch (error) {
        if (closed) return;
        popup.textContent = String(error instanceof Error ? error.message : error);
        return;
      }
      if (closed) return;
      popup.textContent = "";
      const head = doc.createElement("div");
      head.textContent = listing.dir === "" ? "Model folders" : listing.dir;
      head.style.cssText = "padding:4px 8px;font-weight:600;border-bottom:1px solid #ddd;word-break:break-all";
      popup.appendChild(head);
      if (listing.parent !== null && listing.parent !== undefined)
        popup.appendChild(row("..", "", () => void show(listing.parent!)));
      for (const item of listing.entries) popup.appendChild(entryRow(item));
      if (listing.entries.length === 0) {
        const empty = doc.createElement("div");
        empty.textContent = "No matching files";
        empty.style.cssText = "padding:6px 8px;opacity:0.6";
        popup.appendChild(empty);
      }
    };
    const entryRow = (item: FolderEntry): HTMLDivElement =>
      item.isDirectory
        ? row(`${item.name}/`, "", () => void show(item.path))
        : row(item.name, item.sizeBytes == null ? "" : sizeText(item.sizeBytes), () => { close(); pick(item.path); });
    void show(startFolder(entry.value, entry.status));
    return close;
  }
}

function sizeText(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
