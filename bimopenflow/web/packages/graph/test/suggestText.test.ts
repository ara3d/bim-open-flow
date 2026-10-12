import { describe, expect, it } from "vitest";
import { completionOptions } from "../src/suggestText.js";

const COLS = ["name", "count"];

describe("completionOptions", () => {
  it("returns the values as-is when there is no comma", () => {
    expect(completionOptions("", COLS)).toEqual(["name", "count"]);
    expect(completionOptions("na", COLS)).toEqual(["name", "count"]);
  });

  it("completes the token after the last comma, keeping earlier text", () => {
    expect(completionOptions("name,", COLS)).toEqual(["name,name", "name,count"]);
    expect(completionOptions("name,co", COLS)).toEqual(["name,name", "name,count"]);
  });

  it("preserves the whitespace the user typed after the comma", () => {
    expect(completionOptions("name, ", COLS)).toEqual(["name, name", "name, count"]);
    expect(completionOptions("a, b, ", COLS)).toEqual(["a, b, name", "a, b, count"]);
  });
});

describe("column list helpers", () => {
  it("toggles a column in and out", async () => {
    const { toggleColumn } = await import("../src/suggestText.js");
    expect(toggleColumn("", "a")).toBe("a");
    expect(toggleColumn("a, b", "a")).toBe("b");
    expect(toggleColumn("a", "b")).toBe("a,b");
  });

  it("decides from the descriptor's list flag, never from the parameter name", async () => {
    const { isMultiColumn } = await import("../src/suggestText.js");
    expect(isMultiColumn("", true)).toBe(true);
    expect(isMultiColumn("", false)).toBe(false);
    expect(isMultiColumn("", undefined)).toBe(false);
    expect(isMultiColumn("a,b", false)).toBe(true);
    expect(isMultiColumn("a", undefined)).toBe(false);
  });
});
