// @vitest-environment jsdom
import type { NodeDescriptor, PortDescriptor, PortType } from "@bimopenflow/contracts";
import { afterEach, describe, expect, it, vi } from "vitest";
import { installCanvasPalette, paletteClientPoint, paletteKeyOpens } from "../src/canvasPalette.js";
import { parseAnchorId } from "@bimopenflow/graph";

const port = (name: string, type: PortType): PortDescriptor => ({ name, type, optional: false });
const desc = (kind: string, description: string, inputs: PortDescriptor[] = [], outputs: PortDescriptor[] = []): NodeDescriptor => ({
  kind, version: 1, capability: "Pure", inputs, outputs, params: [], description,
});
const catalog = [
  desc("table.sort", "Orders rows", [port("table", "Table")], [port("table", "Table")]),
  desc("chart.bar", "Bar chart", [port("rows", "Table")], []),
  desc("text.join", "Joins text", [port("a", "Text")], [port("text", "Text")]),
];

const disposers: (() => void)[] = [];
afterEach(() => {
  disposers.splice(0).forEach((dispose) => dispose());
  document.body.replaceChildren();
  vi.restoreAllMocks();
});

function setup() {
  const canvas = document.createElement("canvas");
  canvas.tabIndex = 0;
  document.body.append(canvas);
  canvas.focus();
  const onPick = vi.fn();
  const palette = installCanvasPalette(canvas, { getCatalog: () => catalog, onPick });
  disposers.push(() => palette.dispose());
  const input = () => document.querySelector<HTMLInputElement>(".bof-app-palette-search")!;
  const options = () => [...document.querySelectorAll<HTMLElement>("[role=listbox] [role=option]")];
  const kindsShown = () => options().map((o) => o.querySelector(".bof-app-palette-kind")!.textContent);
  const type = (text: string) => {
    input().value = text;
    input().dispatchEvent(new Event("input", { bubbles: true }));
  };
  const key = (k: string) => {
    const event = new KeyboardEvent("keydown", { key: k, bubbles: true, cancelable: true });
    input().dispatchEvent(event);
    return event;
  };
  return { canvas, onPick, palette, input, options, kindsShown, type, key };
}

describe("canvas palette", () => {
  it("opens at the cursor with the search focused and every kind listed with its description", () => {
    const { palette, input, options, kindsShown } = setup();
    palette.open({ x: 120, y: 90 }, { x: 10, y: 20 });
    expect(palette.isOpen()).toBe(true);
    expect(document.activeElement).toBe(input());
    expect(kindsShown()).toEqual(["chart.bar", "table.sort", "text.join"]);
    expect(options()[0]!.querySelector(".bof-app-palette-desc")!.textContent).toBe("Bar chart");
    expect(options()[0]!.getAttribute("aria-selected")).toBe("true");
    const root = document.querySelector<HTMLElement>(".bof-app-palette")!;
    expect([root.style.left, root.style.top]).toEqual(["120px", "90px"]);
    expect(document.getElementById("bof-app-palette-styles")).not.toBeNull();
  });

  it("filters as you type and picks with the arrow keys and Enter", () => {
    const { canvas, palette, kindsShown, type, key, onPick } = setup();
    palette.open({ x: 0, y: 0 }, { x: 10, y: 20 });
    type("t");
    expect(kindsShown()).toEqual(["chart.bar", "table.sort", "text.join"]);
    type("text");
    expect(kindsShown()).toEqual(["text.join"]);
    type("");
    key("ArrowDown");
    key("ArrowDown");
    key("ArrowUp");
    expect(key("Enter").defaultPrevented).toBe(true);
    expect(onPick).toHaveBeenCalledExactlyOnceWith({ desc: catalog[0] }, { x: 10, y: 20 }, undefined);
    expect(palette.isOpen()).toBe(false);
    expect(document.activeElement).toBe(canvas);
  });

  it("wraps around with the arrow keys", () => {
    const { palette, key, onPick } = setup();
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 });
    key("ArrowUp");
    key("Enter");
    expect(onPick.mock.calls[0]![0].desc.kind).toBe("text.join");
  });

  it("filtered by a wire, shows the port and passes the anchor on click", () => {
    const { palette, options, kindsShown, onPick } = setup();
    const from = parseAnchorId("out:sort1.table");
    palette.open({ x: 0, y: 0 }, { x: 5, y: 6 }, { from, type: "Table" });
    expect(kindsShown()).toEqual(["chart.bar", "table.sort"]);
    expect(options()[0]!.querySelector(".bof-app-palette-port")!.textContent).toBe("input rows");
    options()[1]!.querySelector<HTMLElement>(".bof-app-palette-desc")!.click();
    expect(onPick).toHaveBeenCalledExactlyOnceWith({ desc: catalog[0], port: "table" }, { x: 5, y: 6 }, from);
  });

  it("says so when nothing matches, and Enter then does nothing", () => {
    const { palette, type, key, onPick } = setup();
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 }, { from: parseAnchorId("in:x.a"), type: "Boolean" });
    expect(document.querySelector(".bof-app-palette-empty")!.textContent).toBe("No kind can take this wire");
    type("zzz");
    key("Enter");
    expect(onPick).not.toHaveBeenCalled();
  });

  it("closes on Escape without picking and restores focus", () => {
    const { canvas, palette, key, onPick } = setup();
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 });
    const escape = key("Escape");
    expect(escape.defaultPrevented).toBe(true);
    expect(palette.isOpen()).toBe(false);
    expect(document.querySelector(".bof-app-palette")).toBeNull();
    expect(document.activeElement).toBe(canvas);
    expect(onPick).not.toHaveBeenCalled();
  });

  it("closes on a pointerdown outside but not inside", () => {
    const { palette, input } = setup();
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 });
    input().dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
    expect(palette.isOpen()).toBe(true);
    document.body.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
    expect(palette.isOpen()).toBe(false);
  });

  it("reopening replaces the palette", () => {
    const { palette } = setup();
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 });
    palette.open({ x: 30, y: 30 }, { x: 0, y: 0 });
    expect(document.querySelectorAll(".bof-app-palette")).toHaveLength(1);
  });

  it("clamps inside the window", () => {
    const { palette } = setup();
    vi.spyOn(HTMLDivElement.prototype, "getBoundingClientRect").mockReturnValue({ width: 320, height: 200 } as DOMRect);
    palette.open({ x: window.innerWidth - 1, y: window.innerHeight - 1 }, { x: 0, y: 0 });
    const root = document.querySelector<HTMLElement>(".bof-app-palette")!;
    expect(root.style.left).toBe(`${window.innerWidth - 328}px`);
    expect(root.style.top).toBe(`${window.innerHeight - 208}px`);
  });

  it("dispose closes the palette and removes its listeners", () => {
    const { palette } = setup();
    const documentRemove = vi.spyOn(document, "removeEventListener");
    const windowRemove = vi.spyOn(window, "removeEventListener");
    palette.open({ x: 0, y: 0 }, { x: 0, y: 0 });
    palette.dispose();
    expect(palette.isOpen()).toBe(false);
    expect(document.querySelector(".bof-app-palette")).toBeNull();
    expect(documentRemove.mock.calls.map(([name]) => name)).toEqual(["pointerdown"]);
    expect(windowRemove.mock.calls.map(([name]) => name)).toEqual(["blur", "resize"]);
  });
});

describe("palette key", () => {
  const press = (target: EventTarget, init: KeyboardEventInit = {}) => {
    let opens = false;
    const canvas = document.querySelector("canvas")!;
    const listener = (e: Event) => { opens = paletteKeyOpens(e as KeyboardEvent, canvas); };
    document.addEventListener("keydown", listener);
    target.dispatchEvent(new KeyboardEvent("keydown", { key: " ", bubbles: true, ...init }));
    document.removeEventListener("keydown", listener);
    return opens;
  };

  it("opens on a bare Space on the canvas or the page", () => {
    const { canvas } = setup();
    expect(press(canvas)).toBe(true);
    expect(press(document.body)).toBe(true);
  });

  it("leaves Space to fields, buttons, panels, modifiers, and auto-repeat", () => {
    const { canvas } = setup();
    for (const tag of ["input", "textarea", "select", "button", "div"]) {
      const el = document.createElement(tag);
      document.body.append(el);
      expect(press(el), tag).toBe(false);
    }
    expect(press(canvas, { ctrlKey: true })).toBe(false);
    expect(press(canvas, { repeat: true })).toBe(false);
    expect(press(canvas, { key: "a" })).toBe(false);
  });

  it("opens at the pointer over the canvas, else at the canvas centre", () => {
    const { canvas } = setup();
    vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue(
      { left: 100, top: 50, right: 500, bottom: 350, width: 400, height: 300 } as DOMRect);
    expect(paletteClientPoint(canvas, { x: 150, y: 60 })).toEqual({ x: 150, y: 60 });
    expect(paletteClientPoint(canvas, { x: 20, y: 60 })).toEqual({ x: 300, y: 200 });
    expect(paletteClientPoint(canvas, null)).toEqual({ x: 300, y: 200 });
  });

  it("names the key in the search placeholder, and passes no point when opened without one", () => {
    const { palette, input, key, onPick } = setup();
    palette.open({ x: 0, y: 0 }, undefined);
    expect(input().placeholder).toBe("Add a node (Space)");
    key("Enter");
    expect(onPick).toHaveBeenCalledExactlyOnceWith({ desc: catalog[1] }, undefined, undefined);
  });
});
