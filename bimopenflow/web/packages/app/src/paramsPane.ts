// App-owned editable params form, implementing the panes contract so the
// pane area treats it like any other pane. Edits are emitted as
// { kind: "action", action: "setParam", payload: { name, value } } events;
// the pane area maps them to store dispatches (panes never mutate anything).

import type { ParamDescriptor } from "@bimopenflow/contracts";
import type { Pane, PaneContext, PaneEvent, PaneInput } from "@bimopenflow/panes";
import {
  fromDatetimeLocal,
  toDatetimeLocal,
} from "./paramText.js";
import { attachSuggestions, type SuggestFetch } from "./suggestInput.js";
import { displayScale, isNumericParam, numericDisplay, numericFromDisplay, numericLimits, numericValue, paramLabel } from "./numericParam";

function editorFor(
  doc: Document,
  param: ParamDescriptor,
  value: string,
  onChange: (value: string) => void,
  suggest?: { fetch: SuggestFetch; listId: string; onDetach: (fn: () => void) => void },
): HTMLElement {
  if (param.control?.kind === "range") {
    const group = doc.createElement("div");
    group.style.cssText = "display:flex;gap:8px;min-width:0";
    const control = param.control;
    let pair: number[];
    try { pair = JSON.parse(value); } catch { pair = []; }
    if (!Array.isArray(pair) || pair.length !== 2 || !pair.every(Number.isFinite)) pair = [control.min ?? 0, control.max ?? 1];
    const fields = [0,1].map(index => {
      const field = doc.createElement("input");
      field.type = "number";
      field.style.minWidth = "0";
      field.setAttribute("aria-label", `${paramLabel(param.name,param.kind,control)} ${index === 0 ? "start" : "end"}`);
      field.step = String((control.step ?? .01)*displayScale(control));
      field.addEventListener("change", () => {
        const next = numericValue("Fraction",numericFromDisplay(field.value,control),control);
        if (next !== null) {
          pair[index] = index === 0 ? Math.min(Number(next),pair[1]!) : Math.max(Number(next),pair[0]!);
          onChange(JSON.stringify(pair));
        }
        sync();
      });
      group.append(field);
      return field;
    });
    const sync = () => fields.forEach((field,index) => {
      field.value = numericDisplay(String(pair[index]),control);
      field.min = numericDisplay(String(index === 0 ? control.min ?? 0 : pair[0]),control);
      field.max = numericDisplay(String(index === 1 ? control.max ?? 1 : pair[1]),control);
    });
    sync();
    return group;
  }
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
  if (param.kind === "DateTime") {
    const input = doc.createElement("input");
    input.type = "datetime-local";
    input.value = toDatetimeLocal(value);
    input.addEventListener("change", () => onChange(fromDatetimeLocal(input.value)));
    return input;
  }
  const input = doc.createElement("input");
  input.type = param.control?.kind === "color" ? "color" : "text";
  input.value = value;
  if (isNumericParam(param.kind)) {
    input.type = "number";
    const limits = numericLimits(param.kind,param.control);
    const scale = displayScale(param.control);
    input.value = numericDisplay(value,param.control);
    input.step = limits.step === undefined ? "any" : String(limits.step*scale);
    if (limits.min !== undefined) input.min = String(limits.min*scale);
    if (limits.max !== undefined) input.max = String(limits.max*scale);
    input.inputMode = "decimal";
    input.addEventListener("change", () => {
      const canonical = numericValue(param.kind,numericFromDisplay(input.value,param.control),param.control);
      if (canonical === null) input.value = numericDisplay(value,param.control);
      else { input.value = numericDisplay(canonical,param.control); onChange(canonical); }
    });
    return input;
  }
  input.addEventListener("change", () => onChange(input.value));
  if (suggest) suggest.onDetach(attachSuggestions(input, suggest.listId, suggest.fetch));
  return input;
}

/** Editable parameter form for the inspected node. Accepts "inspect" inputs. */
export function createParamsPane(): Pane {
  let root: HTMLElement | null = null;
  let ctx: PaneContext | null = null;
  const handlers: Array<(e: PaneEvent) => void> = [];
  const emit = (e: PaneEvent) => handlers.forEach((h) => h(e));
  let detachers: Array<() => void> = [];

  const detachAll = () => {
    for (const detach of detachers) detach();
    detachers = [];
  };

  const suggestFor = (input: Extract<PaneInput, { kind: "inspect" }>, param: ParamDescriptor) => {
    const request = ctx?.requestSuggestions;
    const nodeId = input.nodeId;
    if (!param.suggest || !request || !nodeId) return undefined;
    return {
      fetch: () => request(nodeId, param.name),
      listId: `bof-pane-suggest-${nodeId}-${param.name}`,
      onDetach: (fn: () => void) => detachers.push(fn),
    };
  };

  const render = (input: Extract<PaneInput, { kind: "inspect" }>) => {
    const doc = root!.ownerDocument;
    detachAll();
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
      label.textContent = paramLabel(param.name,param.kind,param.control);
      grid.appendChild(label);
      grid.appendChild(
        editorFor(doc, param, input.values[param.name] ?? param.default, (value) =>
          emit({ kind: "action", action: "setParam", payload: { name: param.name, value } }),
          suggestFor(input, param)),
      );
    }
    root!.appendChild(grid);
  };

  return {
    mount(el, paneCtx) {
      root = el;
      ctx = paneCtx ?? null;
    },
    update(input) {
      if (!root) throw new Error("update before mount");
      if (input.kind === "inspect") render(input);
    },
    onEvent(handler) {
      handlers.push(handler);
    },
    destroy() {
      detachAll();
      root?.replaceChildren();
      root = null;
      ctx = null;
    },
  };
}
