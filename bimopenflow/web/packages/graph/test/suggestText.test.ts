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
    const { toggleColumn, isMultiColumn } = await import("../src/suggestText.js");
    expect(toggleColumn("", "a")).toBe("a");
    expect(toggleColumn("a, b", "a")).toBe("b");
    expect(toggleColumn("a", "b")).toBe("a,b");
    expect(isMultiColumn("groupBy", "")).toBe(true);
    expect(isMultiColumn("leftKey", "a,b")).toBe(true);
    expect(isMultiColumn("leftKey", "a")).toBe(false);
  });

  it("reads the descriptor's list flag and guesses from the name only without it", async () => {
    const { isMultiColumn } = await import("../src/suggestText.js");
    expect(isMultiColumn("leftKey", "", true)).toBe(true);
    expect(isMultiColumn("groupBy", "", false)).toBe(false);
    expect(isMultiColumn("groupBy", "a,b", false)).toBe(true);
    expect(isMultiColumn("groupBy", "", undefined)).toBe(true);
  });
});
