// Pane-contract conformance: every pane behaves identically through the
// mount/update/onEvent/destroy lifecycle.
import { describe, expect, it } from "vitest";
import type { Pane, PaneInput } from "../src/pane";
import { fakeCtx, makeSlice } from "./helpers";

export interface ContractCase {
  name: string;
  make: () => Pane;
  input: PaneInput;
}

export const tableInput: PaneInput = {
  kind: "table",
  data: makeSlice(
    [
      ["globalId", "Text"],
      ["name", "Text"],
      ["area", "Number"],
    ],
    [
      ["g1", "Wall", 10],
      ["g2", "Door", 2.5],
    ],
  ),
};

export const conformance = ({ name, make, input }: ContractCase): void => {
  describe(`${name} (pane contract)`, () => {
    it("throws on update before mount", () => {
      expect(() => make().update(input)).toThrow(/not mounted/);
    });

    it("mounts into a .bof-panes-root and injects styles once", () => {
      const host = document.createElement("div");
      document.body.appendChild(host);
      const pane = make();
      pane.mount(host, fakeCtx());
      expect(host.querySelector(".bof-panes-root")).not.toBeNull();
      expect(
        document.querySelectorAll("#bof-panes-styles").length,
      ).toBe(1);
      pane.destroy();
      host.remove();
    });

    it("throws on double mount", () => {
      const pane = make();
      pane.mount(document.createElement("div"), fakeCtx());
      expect(() =>
        pane.mount(document.createElement("div"), fakeCtx()),
      ).toThrow(/already mounted/);
      pane.destroy();
    });

    it("accepts its input after mount and renders content", () => {
      const host = document.createElement("div");
      const pane = make();
      pane.mount(host, fakeCtx());
      pane.update(input);
      expect(host.querySelector(".bof-panes-root")!.childNodes.length)
        .toBeGreaterThan(0);
      pane.destroy();
    });

    it("ignores input kinds it does not handle", () => {
      const pane = make();
      pane.mount(document.createElement("div"), fakeCtx());
      pane.update({ kind: "selection", ids: ["nope"] });
      pane.update({
        kind: "nodeState",
        state: { nodeId: "n1", status: "Ok", warnings: [] },
      });
      pane.destroy();
    });

    it("destroy removes the root and is idempotent", () => {
      const host = document.createElement("div");
      const pane = make();
      pane.mount(host, fakeCtx());
      pane.update(input);
      pane.destroy();
      expect(host.childNodes.length).toBe(0);
      pane.destroy();
    });
  });
};
