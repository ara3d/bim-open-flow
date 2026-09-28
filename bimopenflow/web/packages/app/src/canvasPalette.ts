// The canvas palette (TKT-96): a searchable list of node kinds opened at the
// cursor, by a right-click on empty canvas or by a wire dropped there. It
// only reports the pick; adding the node is the caller's store batch.

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import type { AnchorRef } from "./canvasIntents.js";
import { filterPalette, type PaletteEntry } from "./paletteFilter.js";

export interface CanvasPaletteDeps {
  readonly getCatalog: () => readonly NodeDescriptor[];
  /** `at` is the world point passed to `open`; `wire` the dropped wire's anchor. */
  readonly onPick: (entry: PaletteEntry, at: { x: number; y: number }, wire?: AnchorRef) => void;
}

export interface CanvasPalette {
  /** Opens at a CSS-pixel position; `at` is the world point a picked node is placed at. */
  open(client: { x: number; y: number }, at: { x: number; y: number }, wire?: { from: AnchorRef; type: PortType }): void;
  close(): void;
  isOpen(): boolean;
  dispose(): void;
}

const STYLE_ID = "bof-app-palette-styles";
const LIST_ID = "bof-app-palette-list";
/** Minimum gap, in CSS pixels, between the palette and the window edge. */
const EDGE = 8;

function ensurePaletteStyles(doc: Document): void {
  if (doc.getElementById(STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STYLE_ID;
  style.textContent = `
    .bof-app-palette { position: fixed; z-index: 10000; width: 320px; max-width: calc(100vw - ${2 * EDGE}px); box-sizing: border-box;
      padding: 6px; background: var(--bof-app-surface, #fff); color: var(--bof-app-text, #1a1a18);
      border: 1px solid var(--bof-app-border, #e5e3de); border-radius: 8px; box-shadow: 0 5px 20px #13253626;
      font: 13px/1.35 var(--bof-app-font, system-ui, sans-serif); }
    .bof-app-palette-search { display: block; width: 100%; box-sizing: border-box; padding: 6px 8px; margin-bottom: 4px;
      border: 1px solid var(--bof-app-border, #e5e3de); border-radius: 5px; font: inherit; color: inherit; background: inherit; }
    .bof-app-palette-list { max-height: 320px; overflow-y: auto; }
    .bof-app-palette-entry { padding: 5px 8px; border-radius: 4px; cursor: pointer; }
    .bof-app-palette-entry[aria-selected="true"] { background: var(--bof-app-hover, #ecebe4); }
    .bof-app-palette-kind { font-weight: 600; }
    .bof-app-palette-port { margin-left: 6px; color: var(--bof-app-accent, #3b82c4); font-size: 12px; }
    .bof-app-palette-desc { display: block; color: var(--bof-app-dim, #8a8880); font-size: 12px;
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bof-app-palette-empty { padding: 5px 8px; color: var(--bof-app-dim, #8a8880); }
  `;
  doc.head.appendChild(style);
}

type Wire = { from: AnchorRef; type: PortType };

interface OpenState {
  readonly root: HTMLDivElement;
  readonly input: HTMLInputElement;
  readonly list: HTMLDivElement;
  readonly at: { x: number; y: number };
  readonly wire: Wire | undefined;
  readonly previousFocus: HTMLElement | null;
  entries: PaletteEntry[];
  active: number;
}

/** Installs the palette's dismissal listeners; the palette element exists only while open. */
export function installCanvasPalette(canvas: HTMLCanvasElement, deps: CanvasPaletteDeps): CanvasPalette {
  const document = canvas.ownerDocument;
  const window = document.defaultView!;
  let current: OpenState | null = null;

  const close = (restoreFocus = false) => {
    if (!current) return;
    const { root, previousFocus } = current;
    current = null;
    root.remove();
    if (restoreFocus && previousFocus?.isConnected) previousFocus.focus();
  };

  const pick = (index: number) => {
    const entry = current?.entries[index];
    if (!current || !entry) return;
    const { at, wire } = current;
    close(true);
    deps.onPick(entry, at, wire?.from);
  };

  const setActive = (index: number) => {
    if (!current) return;
    current.active = index;
    const options = current.list.querySelectorAll<HTMLElement>("[role=option]");
    options.forEach((o, i) => o.setAttribute("aria-selected", String(i === index)));
    const active = options[index];
    if (active) {
      current.input.setAttribute("aria-activedescendant", active.id);
      active.scrollIntoView?.({ block: "nearest" });
    } else current.input.removeAttribute("aria-activedescendant");
  };

  const option = (entry: PaletteEntry, index: number, wire: Wire | undefined): HTMLDivElement => {
    const el = document.createElement("div");
    el.id = `bof-app-palette-option-${index}`;
    el.className = "bof-app-palette-entry";
    el.setAttribute("role", "option");
    el.dataset["index"] = String(index);
    const span = (cls: string, text: string) => {
      const s = document.createElement("span");
      s.className = cls;
      s.textContent = text;
      el.append(s);
      return s;
    };
    span("bof-app-palette-kind", entry.desc.kind);
    if (entry.port !== undefined && wire)
      span("bof-app-palette-port", `${wire.from.dir === "out" ? "input" : "output"} ${entry.port}`);
    span("bof-app-palette-desc", entry.desc.description).title = entry.desc.description;
    return el;
  };

  const render = () => {
    if (!current) return;
    const { list, wire, input } = current;
    current.entries = filterPalette(deps.getCatalog(), input.value, wire && { dir: wire.from.dir, type: wire.type });
    list.replaceChildren(...current.entries.map((entry, i) => option(entry, i, wire)));
    if (current.entries.length === 0) {
      const empty = document.createElement("div");
      empty.className = "bof-app-palette-empty";
      empty.textContent = wire ? "No kind can take this wire" : "No matching kinds";
      list.append(empty);
    }
    setActive(current.entries.length > 0 ? 0 : -1);
  };

  const optionIndex = (target: EventTarget | null): number => {
    const el = target instanceof window.Element ? target.closest<HTMLElement>("[role=option]") : null;
    return el ? Number(el.dataset["index"]) : -1;
  };

  const inputKeyDown = (event: KeyboardEvent) => {
    if (!current) return;
    const count = current.entries.length;
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (count > 0) setActive((current.active + (event.key === "ArrowDown" ? 1 : count - 1)) % count);
    } else if (event.key === "Enter") {
      event.preventDefault();
      pick(current.active);
    } else if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      close(true);
    } else if (event.key === "Tab") {
      close();
    }
  };

  const open: CanvasPalette["open"] = (client, at, wire) => {
    // Reopening keeps the focus that was current before the first open.
    const previousFocus = current?.previousFocus
      ?? (document.activeElement instanceof window.HTMLElement ? document.activeElement : null);
    close();
    ensurePaletteStyles(document);
    const root = document.createElement("div");
    root.className = "bof-app-palette";
    const input = document.createElement("input");
    input.type = "search";
    input.className = "bof-app-palette-search";
    input.placeholder = wire ? "Add a node for this wire" : "Add a node";
    input.autocomplete = "off";
    input.setAttribute("aria-label", input.placeholder);
    input.setAttribute("aria-controls", LIST_ID);
    const list = document.createElement("div");
    list.id = LIST_ID;
    list.className = "bof-app-palette-list";
    list.setAttribute("role", "listbox");
    list.setAttribute("aria-label", "Node kinds");
    root.append(input, list);
    current = { root, input, list, at, wire, previousFocus, entries: [], active: -1 };
    input.addEventListener("input", render);
    input.addEventListener("keydown", inputKeyDown);
    list.addEventListener("pointermove", (e) => {
      const i = optionIndex(e.target);
      if (i >= 0 && i !== current?.active) setActive(i);
    });
    list.addEventListener("click", (e) => pick(optionIndex(e.target)));
    render();
    document.body.append(root);
    const bounds = root.getBoundingClientRect();
    root.style.left = `${Math.max(EDGE, Math.min(client.x, window.innerWidth - bounds.width - EDGE))}px`;
    root.style.top = `${Math.max(EDGE, Math.min(client.y, window.innerHeight - bounds.height - EDGE))}px`;
    input.focus();
  };

  const outsidePointerDown = (event: PointerEvent) => {
    if (current && !current.root.contains(event.target as Node)) close();
  };
  const dismiss = () => close();
  document.addEventListener("pointerdown", outsidePointerDown, true);
  window.addEventListener("blur", dismiss);
  window.addEventListener("resize", dismiss);

  return {
    open,
    close: () => close(),
    isOpen: () => current !== null,
    dispose: () => {
      close();
      document.removeEventListener("pointerdown", outsidePointerDown, true);
      window.removeEventListener("blur", dismiss);
      window.removeEventListener("resize", dismiss);
    },
  };
}
