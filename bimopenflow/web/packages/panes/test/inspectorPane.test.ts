import { describe, expect, it } from "vitest";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createInspectorPane } from "../src/inspectorPane";
import { conformance } from "./conformance";
import { fakeCtx } from "./helpers";

const node: NodeDescriptor = {
  kind: "check.rule",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "in", type: "Table" }],
  outputs: [{ name: "out", type: "Table" }],
  params: [
    { name: "checkId", kind: "Text", default: "" },
    { name: "expr", kind: "Expression", default: "true" },
  ],
  description: "Rule check",
};

const inspectInput = {
  kind: "inspect" as const,
  node,
  values: { checkId: "NBC-1" },
  state: {
    nodeId: "n1",
    status: "Error" as const,
    error: "boom",
    warnings: ["w1", "w2"],
  },
};

conformance({
  name: "InspectorPane",
  make: () => createInspectorPane(),
  input: inspectInput,
});

describe("InspectorPane", () => {
  const mounted = () => {
    const host = document.createElement("div");
    const pane = createInspectorPane();
    pane.mount(host, fakeCtx());
    pane.update(inspectInput);
    return { host, pane };
  };

  it("renders title, params (value or default), ports, and status", () => {
    const { host, pane } = mounted();
    const text = host.textContent!;
    expect(text).toContain("check.rule v1 (Pure)");
    expect(text).toContain("NBC-1"); // provided value
    expect(text).toContain("true"); // param default
    expect(text).toContain("in");
    expect(text).toContain("out");
    expect(host.querySelector(".bof-panes-error")!.textContent).toBe("boom");
    expect(host.querySelectorAll(".bof-panes-warning").length).toBe(2);
    pane.destroy();
  });

  it("nodeState input refreshes the status section", () => {
    const { host, pane } = mounted();
    pane.update({
      kind: "nodeState",
      state: { nodeId: "n1", status: "Ok", warnings: [] },
    });
    expect(host.textContent).toContain("Ok");
    expect(host.querySelector(".bof-panes-error")).toBeNull();
    expect(host.querySelectorAll(".bof-panes-warning").length).toBe(0);
    pane.destroy();
  });

  it("renders nothing before an inspect input", () => {
    const host = document.createElement("div");
    const pane = createInspectorPane();
    pane.mount(host, fakeCtx());
    pane.update({
      kind: "nodeState",
      state: { nodeId: "n1", status: "Ok", warnings: [] },
    });
    expect(host.querySelector(".bof-panes-root")!.childNodes.length).toBe(0);
    pane.destroy();
  });
});
