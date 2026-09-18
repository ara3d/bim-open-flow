import { describe, expect, it } from "vitest";
import { DEFAULT_ANALYSIS, analysisFromSearch } from "../src/graphDemo.js";

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
