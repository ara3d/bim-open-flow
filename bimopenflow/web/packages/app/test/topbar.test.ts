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

describe("topbar", () => {
  it("offers no node style picker (TKT-277: the chip is the only card style)", () => {
    const root = document.createElement("div");
    createTopbar(root, handlers());
    expect(root.querySelector('select[aria-label="Node style"]')).toBeNull();
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
