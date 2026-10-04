import { describe, expect, it, vi } from "vitest";
import { createTopbar, type TopbarHandlers } from "../src/topbar.js";

const handlers = (extra: Partial<TopbarHandlers> = {}): TopbarHandlers => ({
  onOpenAnalysis: vi.fn(),
  onNewAnalysis: vi.fn(),
  onSave: vi.fn(),
  onRun: vi.fn(),
  onThemeChange: vi.fn(),
  ...extra,
});

const stylePicker = (root: HTMLElement) =>
  root.querySelector<HTMLSelectElement>('select[aria-label="Node style"]');

describe("topbar node style picker", () => {
  it("lists the four styles next to the theme picker and calls the handler on change", () => {
    const root = document.createElement("div");
    const onNodeStyleChange = vi.fn();
    const topbar = createTopbar(root, handlers({ onNodeStyleChange }));
    const select = stylePicker(root)!;
    expect([...select.options].map((o) => o.value)).toEqual(["classic", "banner", "chip", "bar"]);
    expect(select.previousElementSibling?.getAttribute("title")).toBe("Canvas theme");

    select.value = "chip";
    select.dispatchEvent(new Event("change"));
    expect(onNodeStyleChange).toHaveBeenCalledWith("chip");

    topbar.setNodeStyle("bar");
    expect(select.value).toBe("bar");
    expect(onNodeStyleChange).toHaveBeenCalledTimes(1);
  });

  it("shows no picker when the page passes no handler", () => {
    const root = document.createElement("div");
    const topbar = createTopbar(root, handlers());
    expect(stylePicker(root)).toBeNull();
    expect(() => topbar.setNodeStyle("chip")).not.toThrow();
  });

  it("still calls the theme handler", () => {
    const root = document.createElement("div");
    const onThemeChange = vi.fn();
    createTopbar(root, handlers({ onThemeChange }));
    const theme = root.querySelector<HTMLSelectElement>('select[title="Canvas theme"]')!;
    theme.value = "dark";
    theme.dispatchEvent(new Event("change"));
    expect(onThemeChange).toHaveBeenCalledWith("dark");
  });
});
