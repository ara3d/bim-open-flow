// App-owned editable params form, implementing the panes contract so the
// pane area treats it like any other pane. Edits are emitted as
// { kind: "action", action: "setParam", payload: { name, value } } events;
// the pane area maps them to store dispatches (panes never mutate anything).

import type { ParamDescriptor } from "@bimopenflow/contracts";
import type { Pane, PaneEvent, PaneInput } from "@bimopenflow/panes";

function editorFor(
  doc: Document,
  param: ParamDescriptor,
  value: string,
  onChange: (value: string) => void,
): HTMLElement {
  if (param.kind === "Enum") {
    const select = doc.createElement("select");
    for (const option of param.enumValues ?? []) {
      const el = doc.createElement("option");
      el.value = option;
      el.textContent = option;
      el.selected = option === value;
      select.appendChild(el);
    }
    select.addEventListener("change", () => onChange(select.value));
    return select;
  }
  if (param.kind === "Boolean") {
    const box = doc.createElement("input");
    box.type = "checkbox";
    box.checked = value === "true";
    box.addEventListener("change", () => onChange(box.checked ? "true" : "false"));
    return box;
  }
  const input = doc.createElement("input");
  input.type = "text";
  input.value = value;
  input.addEventListener("change", () => onChange(input.value));
  return input;
}

/** Editable parameter form for the inspected node. Accepts "inspect" inputs. */
export function createParamsPane(): Pane {
  let root: HTMLElement | null = null;
  const handlers: Array<(e: PaneEvent) => void> = [];
  const emit = (e: PaneEvent) => handlers.forEach((h) => h(e));

  const render = (input: Extract<PaneInput, { kind: "inspect" }>) => {
    const doc = root!.ownerDocument;
    root!.textContent = "";
    if (input.node.params.length === 0) {
      const empty = doc.createElement("div");
      empty.className = "bof-app-empty";
      empty.textContent = "No parameters.";
      root!.appendChild(empty);
      return;
    }
    const grid = doc.createElement("div");
    grid.className = "bof-app-params";
    for (const param of input.node.params) {
      const label = doc.createElement("label");
      label.textContent = param.name;
      grid.appendChild(label);
      grid.appendChild(
        editorFor(doc, param, input.values[param.name] ?? param.default, (value) =>
          emit({ kind: "action", action: "setParam", payload: { name: param.name, value } })),
      );
    }
    root!.appendChild(grid);
  };

  return {
    mount(el) {
      root = el;
    },
    update(input) {
      if (!root) throw new Error("update before mount");
      if (input.kind === "inspect") render(input);
    },
    onEvent(handler) {
      handlers.push(handler);
    },
    destroy() {
      root?.replaceChildren();
      root = null;
    },
  };
}
