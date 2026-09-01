import { beforeEach, describe, expect, it } from "vitest";
import { defaultCanvasTheme, canvasThemeNames } from "../src/canvasTheme.js";
import { THEME_PREF_KEY, loadThemeChoice, saveThemeChoice } from "../src/themeChoice.js";

beforeEach(() => localStorage.clear());

describe("theme choice persistence", () => {
  it("defaults when nothing is stored", () => {
    expect(loadThemeChoice()).toBe(defaultCanvasTheme);
  });

  it("round-trips a saved choice", () => {
    const other = canvasThemeNames.find((n) => n !== defaultCanvasTheme)!;
    saveThemeChoice(other);
    expect(loadThemeChoice()).toBe(other);
  });

  it("falls back to the default on an unknown stored value", () => {
    localStorage.setItem(THEME_PREF_KEY, "not-a-theme");
    expect(loadThemeChoice()).toBe(defaultCanvasTheme);
  });
});
