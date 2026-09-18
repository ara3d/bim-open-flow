import { describe, expect, it } from "vitest";
import { formatNumber, formatValue } from "../src/format";
import { ensureStyles } from "../src/styles";
import { niceTicks } from "../src/scale";

describe("formatValue", () => {
  it("formats invariantly", () => {
    expect(formatValue(1234567.5, "Number")).toBe("1234567.5");
    expect(formatValue(42, "Integer")).toBe("42");
    expect(formatValue(true, "Boolean")).toBe("true");
    expect(formatValue(false, "Boolean")).toBe("false");
    expect(formatValue("hi", "Text")).toBe("hi");
    expect(formatValue(null, "Number")).toBe("");
    expect(formatValue(undefined, "Text")).toBe("");
  });

  it("shows short doubles as they are", () => {
    expect(formatNumber(0.1)).toBe("0.1");
    expect(formatNumber(-2.5)).toBe("-2.5");
  });

  it("rounds long doubles to six fractional digits and trims trailing zeros", () => {
    expect(formatNumber(48696.79999999999)).toBe("48696.8");
    expect(formatNumber(11761.300000000001)).toBe("11761.3");
    expect(formatNumber(1 / 3)).toBe("0.333333");
    expect(formatNumber(2.5000001)).toBe("2.5");
    expect(formatNumber(-0.0000004)).toBe("-4e-7");
    expect(formatNumber(1e21)).toBe("1e+21");
    expect(formatNumber(NaN)).toBe("NaN");
  });

  it("keeps integers exact", () => {
    expect(formatNumber(5821)).toBe("5821");
    expect(formatNumber(-0)).toBe("0");
    expect(formatValue(1234567890123, "Integer")).toBe("1234567890123");
  });
});

describe("niceTicks", () => {
  it("produces round values covering the domain", () => {
    expect(niceTicks(0, 10)).toEqual([0, 2, 4, 6, 8, 10]);
    expect(niceTicks(-3, 5)).toEqual([-2, 0, 2, 4]);
  });
});

describe("ensureStyles", () => {
  it("injects the stylesheet once", () => {
    ensureStyles(document);
    ensureStyles(document);
    expect(document.querySelectorAll("#bof-viz-styles")).toHaveLength(1);
    expect(document.getElementById("bof-viz-styles")?.textContent).toContain(
      ".bof-viz-root",
    );
  });
});
