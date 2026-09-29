import { describe, expect, it } from "vitest";
import { DEFAULT_ANALYSIS, analysisFromSearch, analysisParam, chooseInitialAnalysis, searchWithAnalysis } from "../src/analysisParam.js";

describe("analysisFromSearch", () => {
  it("reads the analysis query parameter", () => {
    expect(analysisFromSearch("?analysis=color-by-category")).toBe("color-by-category");
  });

  it("trims whitespace and decodes the value", () => {
    expect(analysisFromSearch("?analysis=%20massing-boxes%20")).toBe("massing-boxes");
  });

  it("falls back to the Snowdon toolkit when absent or blank", () => {
    expect(analysisFromSearch("")).toBe(DEFAULT_ANALYSIS);
    expect(analysisFromSearch("?other=1")).toBe(DEFAULT_ANALYSIS);
    expect(analysisFromSearch("?analysis=")).toBe(DEFAULT_ANALYSIS);
    expect(analysisFromSearch("?analysis=%20%20")).toBe(DEFAULT_ANALYSIS);
  });

  it("honours an explicit fallback", () => {
    expect(analysisFromSearch("", "ghost-context")).toBe("ghost-context");
  });
});

describe("analysisParam", () => {
  it("is undefined when absent or blank", () => {
    expect(analysisParam("")).toBeUndefined();
    expect(analysisParam("?analysis=%20")).toBeUndefined();
  });
  it("reads a named analysis", () => {
    expect(analysisParam("?x=1&analysis=nb-s07-own-data-join")).toBe("nb-s07-own-data-join");
  });
});

describe("searchWithAnalysis", () => {
  it("sets the parameter and keeps the others", () => {
    expect(searchWithAnalysis("", "a")).toBe("?analysis=a");
    expect(searchWithAnalysis("?x=1&analysis=old", "new")).toBe("?x=1&analysis=new");
  });
});

describe("chooseInitialAnalysis", () => {
  const ids = ["first", "nb-s07-own-data-join"];
  it("opens the requested analysis when the host has it", () => {
    expect(chooseInitialAnalysis("nb-s07-own-data-join", ids)).toEqual({ open: "nb-s07-own-data-join", missing: undefined });
  });
  it("opens the default and names an unknown id", () => {
    expect(chooseInitialAnalysis("no-such-graph", ids)).toEqual({ open: "first", missing: "no-such-graph" });
  });
  it("opens the default without a parameter", () => {
    expect(chooseInitialAnalysis(undefined, ids)).toEqual({ open: "first", missing: undefined });
  });
});
