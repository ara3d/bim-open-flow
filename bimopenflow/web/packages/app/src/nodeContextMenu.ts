export interface NodeContextMenuDependencies {
  /** Coordinates are CSS pixels relative to the canvas. */
  hitNode(x: number, y: number): string | null;
  onDelete(nodeId: string): void;
}

/** A disposable command menu; graph mutation and hit testing belong to the caller. */
export function installNodeContextMenu(canvas: HTMLCanvasElement, deps: NodeContextMenuDependencies): () => void {
  const document = canvas.ownerDocument;
  const window = document.defaultView!;
  let menu: HTMLDivElement | null = null;
  let previousFocus: HTMLElement | null = null;

  const dismiss = (restoreFocus = false) => {
    menu?.remove();
    menu = null;
    if (restoreFocus && previousFocus?.isConnected) previousFocus.focus();
    previousFocus = null;
  };
  const contextMenu = (event: MouseEvent) => {
    event.preventDefault();
    event.stopPropagation();
    dismiss(true);
    const rect = canvas.getBoundingClientRect();
    const nodeId = deps.hitNode(event.clientX - rect.left, event.clientY - rect.top);
    if (nodeId === null) return;

    previousFocus = document.activeElement instanceof window.HTMLElement ? document.activeElement : null;
    menu = document.createElement("div");
    menu.setAttribute("role", "menu");
    menu.setAttribute("aria-label", "Node actions");
    menu.style.cssText = "position:fixed;z-index:10000;min-width:140px;max-width:calc(100vw - 16px);padding:5px;background:#fff;color:#182431;border:1px solid #c8d2dc;border-radius:8px;box-shadow:0 5px 20px #13253626;box-sizing:border-box;";
    const button = document.createElement("button");
    button.type = "button";
    button.setAttribute("role", "menuitem");
    button.textContent = "Delete node";
    button.style.cssText = "display:block;width:100%;padding:8px 12px;border:0;border-radius:4px;background:#f4f6f8;color:#a52222;font:500 14px/1.4 system-ui,sans-serif;text-align:left;cursor:pointer;";
    button.addEventListener("click", () => {
      dismiss(true);
      deps.onDelete(nodeId);
    });
    menu.append(button);
    document.body.append(menu);
    const bounds = menu.getBoundingClientRect();
    menu.style.left = `${Math.max(8, Math.min(event.clientX, window.innerWidth - bounds.width - 8))}px`;
    menu.style.top = `${Math.max(8, Math.min(event.clientY, window.innerHeight - bounds.height - 8))}px`;
    button.focus();
  };
  const canvasPointerDown = (event: PointerEvent) => {
    if (event.button !== 2) return;
    // Gratify must not interpret the context-menu gesture as a canvas drag.
    event.preventDefault();
    event.stopImmediatePropagation();
  };
  const outsidePointerDown = (event: PointerEvent) => {
    if (menu && !menu.contains(event.target as Node)) dismiss();
  };
  const keyDown = (event: KeyboardEvent) => {
    if (!menu) return;
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      dismiss(true);
    } else if (event.key === "Tab") {
      dismiss(true);
    } else if (["ArrowUp", "ArrowDown", "Home", "End"].includes(event.key)) {
      event.preventDefault();
      menu.querySelector("button")!.focus();
    }
  };
  const blur = () => dismiss();
  canvas.addEventListener("contextmenu", contextMenu);
  canvas.addEventListener("pointerdown", canvasPointerDown, true);
  document.addEventListener("pointerdown", outsidePointerDown, true);
  document.addEventListener("keydown", keyDown, true);
  window.addEventListener("blur", blur);
  window.addEventListener("resize", blur);
  return () => {
    dismiss();
    canvas.removeEventListener("contextmenu", contextMenu);
    canvas.removeEventListener("pointerdown", canvasPointerDown, true);
    document.removeEventListener("pointerdown", outsidePointerDown, true);
    document.removeEventListener("keydown", keyDown, true);
    window.removeEventListener("blur", blur);
    window.removeEventListener("resize", blur);
  };
}
