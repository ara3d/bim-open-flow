// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CLICK_SLOP_PX, HOVER_DELAY_MS, installPortHover } from "../src/portHover.js";

const disposers: (() => void)[] = [];

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  disposers.splice(0).forEach((dispose) => dispose());
  document.body.replaceChildren();
  vi.useRealTimers();
});

function setup(targets: Record<string, string | null> | ((x: number, y: number) => string | null)) {
  const canvas = document.createElement("canvas");
  document.body.append(canvas);
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({ left: 0, top: 0 } as DOMRect);
  const targetAt = typeof targets === "function" ? targets : (x: number, y: number) => targets[`${x},${y}`] ?? null;
  const hover = vi.fn();
  const pin = vi.fn();
  const dispose = installPortHover(canvas, { targetAt, hover, pin });
  disposers.push(dispose);
  return { canvas, hover, pin, dispose };
}

function move(canvas: HTMLCanvasElement, x: number, y: number, buttons = 0) {
  canvas.dispatchEvent(new MouseEvent("pointermove", { clientX: x, clientY: y, buttons, bubbles: true }));
}

function down(canvas: HTMLCanvasElement, x: number, y: number, button = 0) {
  canvas.dispatchEvent(new MouseEvent("pointerdown", { clientX: x, clientY: y, button, bubbles: true }));
}

function up(canvas: HTMLCanvasElement, x: number, y: number, button = 0) {
  canvas.dispatchEvent(new MouseEvent("pointerup", { clientX: x, clientY: y, button, bubbles: true }));
}

describe("installPortHover", () => {
  it("fires hover once after a 300 ms rest, surviving a brief flicker off the target", () => {
    const { canvas, hover } = setup({ "10,10": "a.out" });
    move(canvas, 10, 10); // t=0, onto a.out
    vi.advanceTimersByTime(200);
    move(canvas, 999, 999); // t=200, off
    vi.advanceTimersByTime(50);
    move(canvas, 10, 10); // t=250, back onto a.out
    vi.advanceTimersByTime(299);
    expect(hover).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1); // t=550
    expect(hover).toHaveBeenCalledExactlyOnceWith("a.out");

    canvas.dispatchEvent(new MouseEvent("pointerleave", { bubbles: true }));
    expect(hover).toHaveBeenCalledTimes(2);
    expect(hover).toHaveBeenLastCalledWith(null);
  });

  it("sends no hover call at all while a pointer button is held throughout the same rest", () => {
    const { canvas, hover } = setup({ "10,10": "a.out" });
    move(canvas, 10, 10, 1);
    vi.advanceTimersByTime(200);
    move(canvas, 999, 999, 1);
    vi.advanceTimersByTime(50);
    move(canvas, 10, 10, 1);
    vi.advanceTimersByTime(300);
    expect(hover).not.toHaveBeenCalled();
  });

  it("restarts the timer when moving directly to another target", () => {
    const { canvas, hover } = setup({ "10,10": "a.out", "20,20": "b.out" });
    move(canvas, 10, 10);
    vi.advanceTimersByTime(HOVER_DELAY_MS - 50);
    move(canvas, 20, 20);
    vi.advanceTimersByTime(HOVER_DELAY_MS - 50);
    expect(hover).not.toHaveBeenCalled();
    vi.advanceTimersByTime(50);
    expect(hover).toHaveBeenCalledExactlyOnceWith("b.out");
  });

  it("pins on a press and release within the click slop on a target", () => {
    const { canvas, pin, hover } = setup({ "10,10": "a.out" });
    down(canvas, 10, 10);
    up(canvas, 10 + CLICK_SLOP_PX, 10);
    expect(pin).toHaveBeenCalledExactlyOnceWith("a.out");
    expect(hover).not.toHaveBeenCalled();
  });

  it("does not pin when the release moves past the click slop", () => {
    const { canvas, pin } = setup({ "10,10": "a.out" });
    down(canvas, 10, 10);
    up(canvas, 10 + CLICK_SLOP_PX + 1, 10);
    expect(pin).not.toHaveBeenCalled();
  });

  it("unpins on a press over empty canvas", () => {
    const { canvas, pin } = setup({});
    down(canvas, 500, 500);
    expect(pin).toHaveBeenCalledExactlyOnceWith(null);
    up(canvas, 500, 500);
    expect(pin).toHaveBeenCalledTimes(1);
  });

  it("unpins on Escape", () => {
    const { pin } = setup({});
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    expect(pin).toHaveBeenCalledExactlyOnceWith(null);
  });

  it("removes every listener on dispose", () => {
    const { canvas, hover, pin, dispose } = setup({ "10,10": "a.out" });
    dispose();
    move(canvas, 10, 10);
    vi.advanceTimersByTime(HOVER_DELAY_MS);
    down(canvas, 500, 500);
    up(canvas, 500, 500);
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    expect(hover).not.toHaveBeenCalled();
    expect(pin).not.toHaveBeenCalled();
  });
});
