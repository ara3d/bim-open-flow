import { describe, expect, it } from "vitest";
import type { TableSlice } from "@bimopenflow/contracts";
import { DEFAULT_PAGE_SIZE, makePaneContext, type ResultApi } from "../src/paneContext.js";

const slice = (skip: number, rows: number): TableSlice => ({
  columns: [{ name: "id", type: "Integer"}],
  rows: Array.from({ length: rows }, (_, i) => [skip + i]),
  totalRows: 500,
  skip,
});

function fakeApi(): { api: ResultApi; calls: unknown[][] } {
  const calls: unknown[][] = [];
  return {
    calls,
    api: {
      getResult: (analysisId, nodeId, port, skip, take) => {
        calls.push([analysisId, nodeId, port, skip, take]);
        return Promise.resolve(slice(skip ?? 0, Math.min(take ?? 0, 500)));
      },
      getSuggestions: (analysisId, nodeId, param) => {
        calls.push(["suggest", analysisId, nodeId, param]);
        return Promise.resolve({ status: "Ok" as const, values: [{ value: "name" }] });
      },
      getModelBosUrl: (id) => `/api/models/${encodeURIComponent(id)}/bos`,
    },
  };
}

describe("makePaneContext.requestTable", () => {
  it("routes to getResult with the bound analysis id and default paging", async () => {
    const { api, calls } = fakeApi();
    const ctx = makePaneContext(api, "an1");
    const data = await ctx.requestTable("node1", "out");
    expect(calls).toEqual([["an1", "node1", "out", 0, DEFAULT_PAGE_SIZE]]);
    expect(data.skip).toBe(0);
    expect(data.totalRows).toBe(500);
  });

  it("pages through explicit skip/take", async () => {
    const { api, calls } = fakeApi();
    const ctx = makePaneContext(api, "an1");
    const page = await ctx.requestTable("node1", "out", 200, 100);
    expect(calls[0]).toEqual(["an1", "node1", "out", 200, 100]);
    expect(page.skip).toBe(200);
    expect(page.rows[0]).toEqual([200]);
    expect(page.rows).toHaveLength(100);
  });
});

describe("makePaneContext.resolveAsset", () => {
  it("maps the model: scheme to the host model-bytes endpoint", () => {
    const ctx = makePaneContext(fakeApi().api, "an1");
    expect(ctx.resolveAsset("model:abc")).toBe("/api/models/abc/bos");
  });

  it("passes other urls through", () => {
    const ctx = makePaneContext(fakeApi().api, "an1");
    expect(ctx.resolveAsset("/files/x.glb")).toBe("/files/x.glb");
  });
});
