import { describe, expect, it } from "vitest";
import { freshCopyId, freshNodeId, freshUntitledId, isUntitledId, slugId } from "../src/ids.js";

describe("ids", () => {
  it("derives a node id from the kind's last segment and skips taken ids", () => {
    expect(freshNodeId("source.model", [])).toBe("model1");
    expect(freshNodeId("source.model", ["model1"])).toBe("model2");
  });

  it("numbers untitled flows from 1", () => {
    expect(freshUntitledId(["untitled-1"])).toBe("untitled-2");
  });

  it("names a copy after its source, numbering only from the second copy", () => {
    expect(freshCopyId("door-schedule", ["door-schedule"])).toBe("door-schedule-copy");
    expect(freshCopyId("door-schedule", ["door-schedule", "door-schedule-copy"])).toBe("door-schedule-copy-2");
  });

  it("knows an untitled id", () => {
    expect(isUntitledId("untitled-3")).toBe(true);
    expect(isUntitledId("untitled-copy")).toBe(false);
    expect(isUntitledId(null)).toBe(false);
  });

  it("turns a typed name into a host id", () => {
    expect(slugId("  Door Schedule, per Storey! ")).toBe("door-schedule-per-storey");
    expect(slugId("--")).toBe("");
  });
});
