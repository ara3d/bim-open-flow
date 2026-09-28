import { describe, expect, it } from "vitest";
import { freshCopyId, freshNodeId, freshUntitledId } from "../src/ids.js";

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
});
