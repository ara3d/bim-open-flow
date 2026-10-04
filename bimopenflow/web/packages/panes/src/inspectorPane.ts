import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import type { Pane } from "./pane";
import { definePane } from "./base";

/**
 * Inspector pane: renders a definition list of a node's params, ports, and
 * evaluation status. Plain DOM, styled under the bof-panes- prefix.
 *
 * Inputs: "inspect" sets the node, its param values, and (optionally) its
 * state; "nodeState" replaces the status section of the currently inspected
 * node. Emits no events.
 */
export const createInspectorPane = (): Pane =>
  definePane((root) => {
    let node: NodeDescriptor | null = null;
    let values: Record<string, string> = {};
    let state: NodeState | undefined;

    const render = (): void => {
      const doc = root.ownerDocument;
      root.textContent = "";
      if (!node) return;

      const el = (tag: string, className: string, text?: string): HTMLElement => {
        const e = doc.createElement(tag);
        e.className = className;
        if (text !== undefined) e.textContent = text;
        return e;
      };
      const section = (title: string): void => {
        root.appendChild(el("div", "bof-panes-section", title));
      };
      const dl = (entries: Array<[string, string, string?]>): void => {
        const list = el("dl", "bof-panes-dl");
        for (const [term, detail, cls] of entries) {
          list.appendChild(el("dt", "bof-panes-term", term));
          list.appendChild(el("dd", cls ?? "bof-panes-value", detail));
        }
        root.appendChild(list);
      };

      root.appendChild(
        el("div", "bof-panes-title", `${node.kind} v${node.version} (${node.capability})`),
      );
      if (node.description)
        root.appendChild(el("div", "bof-panes-description", node.description));

      section("Status");
      dl([
        ["status", state?.status ?? "unknown"],
        ...(state?.error ? [["error", state.error, "bof-panes-error"] as [string, string, string]] : []),
      ]);
      for (const warning of state?.warnings ?? [])
        root.appendChild(el("div", "bof-panes-warning", warning));

      if (node.params.length > 0) {
        section("Params");
        dl(node.params.map((p) => [p.name, values[p.name] ?? p.default]));
      }
      if (node.inputs.length > 0) {
        section("Inputs");
        dl(node.inputs.map((p) => [p.name, p.type]));
      }
      if (node.outputs.length > 0) {
        section("Outputs");
        dl(node.outputs.map((p) => [p.name, p.type]));
      }
    };

    return {
      update(input) {
        if (input.kind === "inspect") {
          node = input.node;
          values = input.values;
          state = input.state;
          render();
        } else if (input.kind === "nodeState") {
          state = input.state;
          render();
        }
      },
    };
  });
