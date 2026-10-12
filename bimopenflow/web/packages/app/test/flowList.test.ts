import { describe, expect, it, vi } from "vitest";
import { emptyUntitledFinder, flowGroups, OWN_FLOWS_LABEL, SAMPLES_LABEL } from "../src/flowList.js";

const a = (id: string, graphHash = `h-${id}`) => ({ id, graphHash });
const ids = (groups: ReturnType<typeof flowGroups>) => groups.map((g) => [g.label, g.flows.map((f) => f.id)]);

describe("flowGroups (TKT-253)", () => {
  const list = [a("bfast-buffers"), a("my-doors"), a("untitled-1"), a("untitled-2"), a("voxel-density")];
  const samples = new Set(["bfast-buffers", "voxel-density"]);

  it("lists the user's flows before the samples and leaves out the hidden ones", () => {
    expect(ids(flowGroups(list, samples, new Set(["untitled-1"]), null))).toEqual([
      [OWN_FLOWS_LABEL, ["my-doors", "untitled-2"]],
      [SAMPLES_LABEL, ["bfast-buffers", "voxel-density"]],
    ]);
  });

  it("keeps a hidden flow while it is open, and adds an open flow the host does not list yet", () => {
    expect(ids(flowGroups(list, samples, new Set(["untitled-1"]), "untitled-1"))[0]).toEqual([OWN_FLOWS_LABEL, ["my-doors", "untitled-1", "untitled-2"]]);
    expect(ids(flowGroups(list, samples, new Set(), "untitled-3"))[0]![1]).toContain("untitled-3");
  });

  it("is one unlabelled group on a page with no samples, and none when nothing is listed", () => {
    expect(ids(flowGroups([a("x"), a("y")], new Set(), new Set(), null))).toEqual([[null, ["x", "y"]]]);
    expect(flowGroups([], samples, new Set(), null)).toEqual([]);
  });
});

describe("emptyUntitledFinder", () => {
  const docs: Record<string, string> = {
    "untitled-1": JSON.stringify({ structure: { nodes: [], edges: [] } }),
    "untitled-2": JSON.stringify({ structure: { nodes: [], edges: [] } }),
    "untitled-3": JSON.stringify({ structure: { nodes: [{ id: "n", kind: "k" }], edges: [] } }),
    "bfast-buffers": JSON.stringify({ structure: { nodes: [], edges: [] } }),
  };

  it("finds the untitled flows with no nodes, reading one document per graph hash", async () => {
    const getAnalysis = vi.fn(async (id: string) => docs[id]!);
    const find = emptyUntitledFinder(getAnalysis);
    const list = [a("untitled-1", "empty"), a("untitled-2", "empty"), a("untitled-3", "full"), a("bfast-buffers", "empty")];
    expect([...await find(list)]).toEqual(["untitled-1", "untitled-2"]);
    expect(getAnalysis).toHaveBeenCalledTimes(2);
    await find(list);
    expect(getAnalysis).toHaveBeenCalledTimes(2);
  });

  it("keeps a flow it cannot read", async () => {
    const find = emptyUntitledFinder(async () => { throw new Error("404"); });
    expect([...await find([a("untitled-1")])]).toEqual([]);
  });
});
