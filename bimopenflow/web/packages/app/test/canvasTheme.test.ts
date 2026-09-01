import { describe, expect, it } from "vitest";
import { themes, tokens, type Color } from "gratify";
import type { NodeStatus } from "@bimopenflow/contracts";
import {
  applyCanvasTheme,
  canvasColors,
  canvasThemeNames,
  canvasThemes,
  currentCanvasTheme,
  defaultCanvasTheme,
  isCanvasThemeName,
} from "../src/canvasTheme.js";

const tokenKeys = [
  "bg", "surface", "surfaceHi", "muted", "text", "textDim", "textBright",
  "accent", "accent2", "danger",
] as const;

const statusKeys: NodeStatus[] = ["Ok", "Unready", "EffectPending", "Unavailable", "Error"];

const extraKeys = ["status", "wire", "wireSelected", "wireShadow", "rubberSnap", "gridDot"] as const;

const isColor = (c: unknown): c is Color =>
  typeof c === "object" && c !== null &&
  ["r", "g", "b", "a"].every((k) => typeof (c as Record<string, unknown>)[k] === "number");

describe("canvas theme table", () => {
  it("defines every declared theme name", () => {
    for (const name of canvasThemeNames) expect(canvasThemes[name]).toBeDefined();
    expect(isCanvasThemeName(defaultCanvasTheme)).toBe(true);
    expect(isCanvasThemeName("nope")).toBe(false);
  });

  it("every palette defines every gratify token as a color", () => {
    for (const name of canvasThemeNames)
      for (const key of tokenKeys)
        expect(isColor(canvasThemes[name].palette[key]), `${name}.${key}`).toBe(true);
  });

  it("palettes cover exactly the gratify token set (no stale extras)", () => {
    const gratifyKeys = Object.keys(tokens).filter((k) => k !== "mix").sort();
    for (const name of canvasThemeNames)
      expect(Object.keys(canvasThemes[name].palette).sort()).toEqual(gratifyKeys);
  });

  it("every theme maps every extra color, incl. all node statuses", () => {
    for (const name of canvasThemeNames) {
      const extras = canvasThemes[name].extras;
      for (const key of extraKeys) expect(extras[key], `${name}.${key}`).toBeDefined();
      for (const s of statusKeys) expect(isColor(extras.status[s]), `${name}.status.${s}`).toBe(true);
    }
  });

  it("registers each theme with gratify under a bof- prefix", () => {
    for (const name of canvasThemeNames)
      expect(themes[`bof-${name}`]).toBe(canvasThemes[name].palette);
  });
});

describe("applyCanvasTheme", () => {
  it("switches the extras canvasParts reads", () => {
    applyCanvasTheme("dark");
    expect(currentCanvasTheme()).toBe("dark");
    expect(canvasColors()).toBe(canvasThemes.dark.extras);
    applyCanvasTheme("light");
    expect(canvasColors()).toBe(canvasThemes.light.extras);
  });

  it("light is the default", () => {
    expect(defaultCanvasTheme).toBe("light");
    expect(canvasThemeNames[0]).toBe("light");
  });
});
