// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { installNodeContextMenu } from "../src/nodeContextMenu.js";

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
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({ left: 50, top: 80 } as DOMRect);
  const hitNode = vi.fn<(x: number, y: number) => string | null>().mockReturnValue("node-a");
  const onDelete = vi.fn();
  const dispose = installNodeContextMenu(canvas, { hitNode, onDelete });
  disposers.push(dispose);
  const open = (x = 130, y = 160) => {
    const event = new MouseEvent("contextmenu", { bubbles: true, cancelable: true, clientX: x, clientY: y });
    canvas.dispatchEvent(event);
    return event;
  };
  return { canvas, hitNode, onDelete, open, dispose };
}

describe("node context menu", () => {
  it("resolves the node in canvas coordinates and invokes Delete once", () => {
    const { canvas, hitNode, onDelete, open } = setup();
    expect(open().defaultPrevented).toBe(true);
    expect(hitNode).toHaveBeenCalledWith(80, 80);
    const button = document.querySelector<HTMLButtonElement>('[role="menuitem"]')!;
    expect(button.textContent).toBe("Delete node");
    expect(document.activeElement).toBe(button);
    button.click();
    expect(onDelete).toHaveBeenCalledExactlyOnceWith("node-a");
    expect(document.querySelector('[role="menu"]')).toBeNull();
    expect(document.activeElement).toBe(canvas);
  });

  it("replaces the menu for another node and does not offer actions on empty canvas", () => {
    const { hitNode, onDelete, open } = setup();
    open();
    hitNode.mockReturnValue("node-b");
    open();
    expect(document.querySelectorAll('[role="menu"]')).toHaveLength(1);
    document.querySelector<HTMLButtonElement>("[role=menuitem]")!.click();
    expect(onDelete).toHaveBeenCalledExactlyOnceWith("node-b");
    hitNode.mockReturnValue(null);
    expect(open().defaultPrevented).toBe(true);
    expect(document.querySelector('[role="menu"]')).toBeNull();
  });

  it.each(["Escape", "Tab", "outside", "blur", "resize"])("dismisses on %s without deleting", (gesture) => {
    const { open, onDelete } = setup();
    open();
    if (gesture === "outside") document.body.dispatchEvent(new MouseEvent("pointerdown", { bubbles: true }));
    else if (gesture === "blur" || gesture === "resize") window.dispatchEvent(new Event(gesture));
    else document.dispatchEvent(new KeyboardEvent("keydown", { key: gesture, bubbles: true }));
    expect(document.querySelector('[role="menu"]')).toBeNull();
    expect(onDelete).not.toHaveBeenCalled();
  });

  it("prevents right-button canvas dragging while allowing primary-button interactions", () => {
    const { canvas } = setup();
    const drag = vi.fn();
    canvas.addEventListener("pointerdown", drag);
    const right = new MouseEvent("pointerdown", { button: 2, cancelable: true });
    canvas.dispatchEvent(right);
    expect(right.defaultPrevented).toBe(true);
    expect(drag).not.toHaveBeenCalled();
    canvas.dispatchEvent(new MouseEvent("pointerdown", { button: 0 }));
    expect(drag).toHaveBeenCalledOnce();
  });

  it("clamps the menu inside the viewport", () => {
    const { open } = setup();
    vi.spyOn(HTMLDivElement.prototype, "getBoundingClientRect").mockReturnValue({ width: 150, height: 50 } as DOMRect);
    open(window.innerWidth - 1, window.innerHeight - 1);
    const menu = document.querySelector<HTMLDivElement>("[role=menu]")!;
    expect(menu.style.left).toBe(`${window.innerWidth - 158}px`);
    expect(menu.style.top).toBe(`${window.innerHeight - 58}px`);
  });

  it("removes the menu and all listeners on disposal", () => {
    const { canvas, open, dispose, hitNode } = setup();
    const canvasRemove = vi.spyOn(canvas, "removeEventListener");
    const documentRemove = vi.spyOn(document, "removeEventListener");
    const windowRemove = vi.spyOn(window, "removeEventListener");
    open();
    dispose();
    expect(document.querySelector('[role="menu"]')).toBeNull();
    expect(open().defaultPrevented).toBe(false);
    expect(hitNode).toHaveBeenCalledOnce();
    expect(canvasRemove.mock.calls.map(([name]) => name)).toEqual(["contextmenu", "pointerdown"]);
    expect(documentRemove.mock.calls.map(([name]) => name)).toEqual(["pointerdown", "keydown"]);
    expect(windowRemove.mock.calls.map(([name]) => name)).toEqual(["blur", "resize"]);
  });
});
