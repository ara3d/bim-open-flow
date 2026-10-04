import { describe, expect, it } from "vitest";
import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import {
  backToAnswer,
  initialShownChoice,
  resolveShown,
  showOverride,
  togglePin,
  type ShownChoice,
} from "../src/shownChoice.js";

const doc = (nodeKinds: Record<string, string>, edges: [string, string][] = []): GraphDocument => ({
  formatVersion: "0.1.0",
  structure: {
    nodes: Object.entries(nodeKinds).map(([id, kind]) => ({ id, kind, version: 1 })),
    edges: edges.map(([from, to]) => ({ from, to })),
  },
  values: {},
  layout: {},
});

const tableDesc: NodeDescriptor = {
  kind: "table.select",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Relation", optional: false }],
  outputs: [{ name: "out", type: "Table", optional: false }],
  params: [],
  description: "",
};

const catalog = new Map([["table.select", tableDesc]]);
const ok: NodeState = { nodeId: "", status: "Ok", warnings: [] };

// source -> mid -> sink: sink is the only terminal, so it is the answer.
const linear = doc(
  { source: "table.select", mid: "table.select", sink: "table.select" },
  [["source.out", "mid.in"], ["mid.out", "sink.in"]],
);
const evalState = { source: ok, mid: ok, sink: ok };

describe("resolveShown", () => {
  it("follows the answer node when nothing is pinned or overridden", () => {
    const choice = initialShownChoice();
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "sink", isAnswer: true });
  });

  it("an override wins over the answer and is not the answer", () => {
    const choice = initialShownChoice();
    showOverride(choice, "mid");
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "mid", isAnswer: false });
  });

  it("drops an override once its node is gone from the document", () => {
    const choice = initialShownChoice();
    showOverride(choice, "gone");
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "sink", isAnswer: true });
    expect(choice.overrideId).toBeNull();
  });

  it("back-to-answer clears an override", () => {
    const choice = initialShownChoice();
    showOverride(choice, "mid");
    backToAnswer(choice);
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "sink", isAnswer: true });
  });

  it("a pin freezes on the node last shown, even once a different node would rank as the answer", () => {
    const choice = initialShownChoice();
    resolveShown(linear, evalState, catalog, choice); // shows "sink"
    showOverride(choice, "mid");
    resolveShown(linear, evalState, catalog, choice); // shows "mid"
    togglePin(choice, "mid");
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "mid", isAnswer: false });

    // pinned "mid" is not the answer ("sink" is), so isAnswer stays false even after
    // the override clears: the pin, not the answer, is why "mid" is still shown.
    choice.overrideId = null;
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "mid", isAnswer: false });
  });

  it("un-pinning resumes following the answer", () => {
    const choice = initialShownChoice();
    resolveShown(linear, evalState, catalog, choice);
    togglePin(choice, "sink");
    togglePin(choice, "sink");
    expect(choice.pinned).toBe(false);
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "sink", isAnswer: true });
  });

  it("auto-unpins once the pinned node is removed from the document", () => {
    const choice: ShownChoice = { overrideId: null, pinned: true, pinnedId: "gone", lastAnswer: "sink" };
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "sink", isAnswer: true });
    expect(choice.pinned).toBe(false);
  });

  it("showing a new node while pinned drops the pin", () => {
    const choice: ShownChoice = { overrideId: null, pinned: true, pinnedId: "sink", lastAnswer: "sink" };
    showOverride(choice, "mid");
    expect(choice.pinned).toBe(false);
    expect(resolveShown(linear, evalState, catalog, choice)).toEqual({ id: "mid", isAnswer: false });
  });
});
