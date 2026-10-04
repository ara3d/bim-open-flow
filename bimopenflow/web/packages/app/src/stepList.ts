// The step list: the open flow read as numbered steps in dataflow order, the
// way Power Query lists applied steps (TKT-95). stepListModel is pure;
// createStepList paints it and reports a click. Read-only: selecting a step
// is the only gesture, and the caller turns it into a store dispatch.

import type { NodeDescriptor, NodeStatus } from "@bimopenflow/contracts";
import type { State } from "@bimopenflow/state";
import { previewText } from "@bimopenflow/graph";
import { dataflowOrder, feeders } from "./graphOrder.js";
import { nodeTitle } from "@bimopenflow/graph";
import { nodeBadge } from "@bimopenflow/graph";
import { firstTableOutput } from "@bimopenflow/client";
import { fileName } from "@bimopenflow/graph";
import type { PortResultsView } from "@bimopenflow/graph";

export interface Step {
  readonly index: number;          // 1-based
  readonly nodeId: string;
  readonly title: string;          // nodeTitle(kind) from graphPreview.ts
  readonly kind: string;
  /** What the step does: the first sentence of the catalog's description,
   *  so the list reads as a story; "" for a kind the catalog lacks. */
  readonly description: string;
  readonly summary: string;        // "table = doors · limit = 10"; "" when no params set
  readonly status?: NodeStatus;
  readonly badge?: string;         // nodeBadge text
  readonly rows?: number;          // first Table/Relation output's count when known
  readonly from: readonly number[]; // step indexes that feed this one, when more than the previous step
  readonly selected: boolean;
}

/** Longest parameter value shown in a summary, ellipsis included. */
export const SUMMARY_VALUE_CHARS = 40;

/** "name = value" for each parameter the document sets, in catalog order,
 *  joined by " · ". A kind missing from the catalog lists the document's
 *  values in stored order. */
function paramSummary(desc: NodeDescriptor | undefined, values: Readonly<Record<string, string>>): string {
  const params: readonly { readonly name: string; readonly kind?: string }[] = desc
    ? desc.params.filter((p) => values[p.name] !== undefined)
    : Object.keys(values).map((name) => ({ name }));
  return params
    .map((p) => {
      const raw = values[p.name]!;
      const shown = p.kind === "FilePath" ? fileName(raw) : raw;
      return `${p.name} = ${previewText(shown, SUMMARY_VALUE_CHARS)}`;
    })
    .join(" · ");
}

/** The first sentence (or line) of a catalog description; the whole text
 *  when it has no sentence end. */
export const firstSentence = (text: string): string => {
  const line = text.split("\n", 1)[0]!.trim();
  const end = line.search(/[.;:]\s/);
  return end < 0 ? line : line.slice(0, end + 1);
};

export function stepListModel(
  state: State,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  results: PortResultsView,
): Step[] {
  const document = state.document;
  const order = dataflowOrder(document);
  const indexOf = new Map(order.map((id, i) => [id, i + 1]));
  const kindOf = new Map(document.structure.nodes.map((n) => [n.id, n.kind]));
  const badgeGraph = { edges: document.structure.edges, evalState: state.evalState };
  return order.map((nodeId, i) => {
    const index = i + 1;
    const kind = kindOf.get(nodeId)!;
    const desc = catalog.get(kind);
    // feeders re-sorts the graph per node; flows are a few dozen nodes at most.
    const fromIndexes = feeders(document, nodeId).map((id) => indexOf.get(id)!);
    const onlyPrevious = fromIndexes.length === 1 && fromIndexes[0] === index - 1;
    const port = firstTableOutput(desc);
    const rows = port ? results.counts.get(`${nodeId}.${port.name}`)?.rows : undefined;
    const badge = nodeBadge(badgeGraph, nodeId);
    return {
      index,
      nodeId,
      title: nodeTitle(kind),
      kind,
      description: firstSentence(desc?.description ?? ""),
      summary: paramSummary(desc, document.values[nodeId] ?? {}),
      ...(badge ? { status: badge.status, badge: badge.text } : {}),
      ...(rows !== undefined ? { rows } : {}),
      from: onlyPrevious ? [] : fromIndexes,
      selected: state.selection.includes(nodeId),
    };
  });
}

export interface StepListDeps { readonly onSelect: (nodeId: string) => void }
export interface StepList { render(steps: readonly Step[]): void; dispose(): void }

const STEP_LIST_STYLE_ID = "bof-app-steps-styles";

function ensureStepListStyles(doc: Document): void {
  if (doc.getElementById(STEP_LIST_STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = STEP_LIST_STYLE_ID;
  style.textContent = `
    .bof-app-steps-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 2px; }
    .bof-app-steps-step {
      display: grid; grid-template-columns: 1.6em minmax(0, 1fr); gap: 0 4px; width: 100%;
      text-align: left; font: inherit; color: inherit; background: none; cursor: pointer;
      padding: 4px 6px; border-radius: 4px; border: 1px solid transparent;
    }
    .bof-app-steps-step:hover { background: var(--bof-app-hover); }
    .bof-app-steps-step.bof-app-steps-selected { border-color: var(--bof-app-accent); }
    .bof-app-steps-index { color: var(--bof-app-dim); grid-row: span 4; }
    .bof-app-steps-description { color: var(--bof-app-text); opacity: 0.8; }
    .bof-app-steps-summary, .bof-app-steps-rows { font-family: var(--bof-app-mono, monospace); }
    .bof-app-steps-title { font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .bof-app-steps-id { color: var(--bof-app-dim); font: 11px var(--bof-app-mono, monospace); margin-left: 4px; }
    .bof-app-steps-description, .bof-app-steps-summary, .bof-app-steps-meta {
      grid-column: 2; font-size: 11px; color: var(--bof-app-dim);
      overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    .bof-app-steps-status-Ok { color: var(--bof-app-green); }
    .bof-app-steps-status-Error { color: var(--bof-app-red); }
    .bof-app-steps-status-Unready, .bof-app-steps-status-Unavailable, .bof-app-steps-status-EffectPending { color: var(--bof-app-amber); }
    .bof-app-steps-empty { color: var(--bof-app-dim); font-size: 11px; padding: 4px 6px; }
  `;
  doc.head.appendChild(style);
}

const rowsText = (rows: number): string => `${rows.toLocaleString("en-US")} ${rows === 1 ? "row" : "rows"}`;

export function createStepList(host: HTMLElement, deps: StepListDeps): StepList {
  const doc = host.ownerDocument;
  ensureStepListStyles(doc);
  const list = doc.createElement("ol");
  list.className = "bof-app-steps-list";
  host.appendChild(list);

  const span = (className: string, text: string): HTMLSpanElement => {
    const el = doc.createElement("span");
    el.className = className;
    el.textContent = text;
    return el;
  };

  // The line under the summary: which earlier steps feed this one, the
  // status badge, and the row count, each only when there is one.
  const metaLine = (step: Step): HTMLSpanElement | null => {
    const parts = [
      ...(step.from.length > 0 ? [span("bof-app-steps-from", `from ${step.from.join(", ")}`)] : []),
      ...(step.badge ? [span(`bof-app-steps-status bof-app-steps-status-${step.status}`, step.badge)] : []),
      ...(step.rows !== undefined ? [span("bof-app-steps-rows", rowsText(step.rows))] : []),
    ];
    if (parts.length === 0) return null;
    const line = span("bof-app-steps-meta", "");
    parts.forEach((el, i) => {
      if (i > 0) line.appendChild(doc.createTextNode(" · "));
      line.appendChild(el);
    });
    return line;
  };

  const stepItem = (step: Step): HTMLLIElement => {
    const button = doc.createElement("button");
    button.type = "button";
    button.className = "bof-app-steps-step" + (step.selected ? " bof-app-steps-selected" : "");
    button.dataset.nodeId = step.nodeId;
    if (step.selected) button.setAttribute("aria-current", "step");
    button.title = step.kind;
    button.appendChild(span("bof-app-steps-index", `${step.index}.`));
    const title = span("bof-app-steps-title", step.title);
    title.appendChild(span("bof-app-steps-id", step.nodeId));
    button.appendChild(title);
    if (step.description) {
      const description = span("bof-app-steps-description", step.description);
      description.title = step.description;
      button.appendChild(description);
    }
    if (step.summary) {
      const summary = span("bof-app-steps-summary", step.summary);
      summary.title = step.summary;
      button.appendChild(summary);
    }
    const meta = metaLine(step);
    if (meta) button.appendChild(meta);
    button.addEventListener("click", () => deps.onSelect(step.nodeId));
    const item = doc.createElement("li");
    item.appendChild(button);
    return item;
  };

  return {
    render(steps) {
      list.textContent = "";
      if (steps.length === 0) {
        const empty = doc.createElement("li");
        empty.className = "bof-app-steps-empty";
        empty.textContent = "No steps in this flow yet";
        list.appendChild(empty);
        return;
      }
      for (const step of steps) list.appendChild(stepItem(step));
    },
    dispose() {
      list.remove();
    },
  };
}
