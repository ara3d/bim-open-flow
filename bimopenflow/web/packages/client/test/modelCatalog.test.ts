import { describe, expect, it, vi } from "vitest";
import type { ModelSummary } from "@bimopenflow/contracts";
import { modelCatalog } from "../src/modelCatalog";

const models = [{ id: "source", sourcePath: "models/source.bos" }] as ModelSummary[];

describe("model catalog loading", () => {
  it("shares an in-flight fetch between pane updates and reuses the result", async () => {
    let release!: (models: ModelSummary[]) => void;
    const list = vi.fn(() => new Promise<ModelSummary[]>(resolve => { release = resolve; }));
    const resolve = modelCatalog(list);
    const first = resolve("source.bos");
    const second = resolve("source.bos");
    expect(list).toHaveBeenCalledTimes(1);
    release(models);
    expect(await Promise.all([first, second])).toEqual(["source", "source"]);
    expect(await resolve("source.bos")).toBe("source");
    expect(list).toHaveBeenCalledTimes(1);
  });
  it("refreshes for a newly available model", async () => {
    const list = vi.fn().mockResolvedValueOnce([]).mockResolvedValue(models);
    expect(await modelCatalog(list)("source.bos")).toBe("source");
    expect(list).toHaveBeenCalledTimes(2);
  });
  it("does not cache failed requests", async () => {
    const list = vi.fn().mockRejectedValueOnce(new Error("offline")).mockResolvedValue(models);
    const resolve = modelCatalog(list);
    await expect(resolve("source.bos")).rejects.toThrow("offline");
    expect(await resolve("source.bos")).toBe("source");
  });
});
