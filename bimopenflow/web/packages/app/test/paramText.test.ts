import { describe, expect, it } from "vitest";
import {
  fromDatetimeLocal,
  normalizeInteger,
  normalizeNumber,
  toDatetimeLocal,
} from "../src/paramText.js";

describe("normalizeInteger", () => {
  it("accepts whole numbers and rejects everything else", () => {
    expect(normalizeInteger(" 42 ")).toBe("42");
    expect(normalizeInteger("-7")).toBe("-7");
    expect(normalizeInteger("9223372036854775807")).toBe("9223372036854775807");
    expect(normalizeInteger("1.5")).toBeNull();
    expect(normalizeInteger("abc")).toBeNull();
    expect(normalizeInteger("")).toBeNull();
  });
});

describe("normalizeNumber", () => {
  it("accepts finite numbers and canonicalizes", () => {
    expect(normalizeNumber("0.10")).toBe("0.1");
    expect(normalizeNumber("-3e2")).toBe("-300");
    expect(normalizeNumber("nope")).toBeNull();
    expect(normalizeNumber("")).toBeNull();
  });
});

describe("DateTime round trip", () => {
  it("date-only canonical <-> datetime-local", () => {
    expect(toDatetimeLocal("2026-09-01")).toBe("2026-09-01T00:00");
    expect(fromDatetimeLocal("2026-09-01T00:00")).toBe("2026-09-01");
  });

  it("full datetime canonical <-> datetime-local", () => {
    expect(toDatetimeLocal("2026-09-01T14:30:00")).toBe("2026-09-01T14:30");
    expect(fromDatetimeLocal("2026-09-01T14:30")).toBe("2026-09-01T14:30:00");
  });

  it("empty means unset in both directions", () => {
    expect(toDatetimeLocal("")).toBe("");
    expect(fromDatetimeLocal("")).toBe("");
  });
});
