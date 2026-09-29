// A press on the canvas takes focus from a text box in the host's chrome
// (TKT-103), without scrolling the page, and leaves a host's tabindex alone.

import { afterEach, describe, expect, it, vi } from "vitest";
import { installCanvasFocus } from "../src/canvasFocus.js";

function setup(tabIndex?: number) {
  const input = document.createElement("input");
  const canvas = document.createElement("canvas");
  if (tabIndex !== undefined) canvas.tabIndex = tabIndex;
  document.body.append(input, canvas);
  input.focus();
  return { input, canvas, dispose: installCanvasFocus(canvas) };
}

const press = (canvas: HTMLCanvasElement) =>
  canvas.dispatchEvent(new Event("pointerdown", { bubbles: true }));

afterEach(() => { document.body.innerHTML = ""; });

describe("installCanvasFocus", () => {
  it("moves focus from a chrome text box to the canvas on pointerdown", () => {
    const { input, canvas } = setup();
    expect(document.activeElement).toBe(input);
    press(canvas);
    expect(document.activeElement).toBe(canvas);
  });

  it("focuses with preventScroll, so an embedded canvas never scrolls the page", () => {
    const { canvas } = setup();
    const focus = vi.spyOn(canvas, "focus");
    press(canvas);
    expect(focus).toHaveBeenCalledWith({ preventScroll: true });
  });

  it("makes a plain canvas focusable outside the tab order and keeps a host's tabindex", () => {
    expect(setup().canvas.tabIndex).toBe(-1);
    expect(setup(0).canvas.tabIndex).toBe(0);
  });

  it("stops taking focus once disposed", () => {
    const { input, canvas, dispose } = setup();
    dispose();
    press(canvas);
    expect(document.activeElement).toBe(input);
  });
});
