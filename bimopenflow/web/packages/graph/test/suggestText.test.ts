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
