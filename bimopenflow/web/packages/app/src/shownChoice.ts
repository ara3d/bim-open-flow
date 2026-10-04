// Resolves which node the pane area shows (TKT-81): the flow's answer by
// default, frozen by a pin, or replaced by an explicit "show" request
// (double-click, the pane header's back button). Pure over the document and
// eval state plus a small mutable choice record, so the pin/override/back
// interactions are testable without a store or a canvas. app.ts owns one
// ShownChoice per open analysis and calls these on every store update and on
// every pin/show/back gesture.

import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import type { GraphDocument } from "@bimopenflow/state";
import { defaultShownNode } from "./defaultShown.js";

export interface ShownChoice {
  /** The node an explicit "show" gesture asked for; null when following the answer. */
  overrideId: string | null;
  /** True once the pane is frozen on `pinnedId` regardless of the answer or an override. */
  pinned: boolean;
  /** The node id the pin freezes on; set when `pinned` turns on, cleared when it turns off. */
  pinnedId: string | null;
  /** The answer's own stickiness pointer, threaded through to defaultShownNode. */
  lastAnswer: string | null;
}

/** A choice that follows the flow's answer node, pinned to nothing. */
export const initialShownChoice = (): ShownChoice => ({
  overrideId: null,
  pinned: false,
  pinnedId: null,
  lastAnswer: null,
});

export interface ShownResolution {
  /** The node to show, or null when the graph has no answer. */
  id: string | null;
  /** True when `id` is the flow's answer, not an explicit override. */
  isAnswer: boolean;
}

const exists = (document: GraphDocument, id: string): boolean =>
  document.structure.nodes.some((n) => n.id === id);

/**
 * Resolves the shown node, mutating `choice` to drop ids that vanished from
 * the document and to advance the answer's stickiness pointer. Precedence:
 * a pin wins over an override, which wins over the plain answer.
 */
export function resolveShown(
  document: GraphDocument,
  evalState: Readonly<Record<string, NodeState>>,
  catalog: ReadonlyMap<string, NodeDescriptor>,
  choice: ShownChoice,
): ShownResolution {
  if (choice.pinnedId !== null && !exists(document, choice.pinnedId)) {
    choice.pinned = false;
    choice.pinnedId = null;
  }
  if (choice.overrideId !== null && !exists(document, choice.overrideId)) choice.overrideId = null;

  const answer = defaultShownNode(document, evalState, catalog, choice.lastAnswer);
  choice.lastAnswer = answer;

  if (choice.pinned && choice.pinnedId !== null)
    return { id: choice.pinnedId, isAnswer: choice.overrideId === null && choice.pinnedId === answer };
  if (choice.overrideId !== null) return { id: choice.overrideId, isAnswer: false };
  return { id: answer, isAnswer: true };
}

/** Shows `nodeId` explicitly (double-click, a Show action); drops any pin,
 *  since a pin's target no longer matches what the user just asked to see. */
export function showOverride(choice: ShownChoice, nodeId: string): void {
  choice.overrideId = nodeId;
  choice.pinned = false;
  choice.pinnedId = null;
}

/** Clears an override and any pin, returning to the plain answer. */
export function backToAnswer(choice: ShownChoice): void {
  choice.overrideId = null;
  choice.pinned = false;
  choice.pinnedId = null;
}

/** Toggles the pin, freezing on `shownId` (the node currently on screen). */
export function togglePin(choice: ShownChoice, shownId: string | null): void {
  choice.pinned = !choice.pinned;
  choice.pinnedId = choice.pinned ? shownId : null;
}
