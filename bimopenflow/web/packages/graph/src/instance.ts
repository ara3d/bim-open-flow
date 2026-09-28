// Everything one mounted canvas owns that used to be module-level state:
// the intent dispatch the DOM side enters the runtime through, the island
// input per parameter row, the long-value and note editors, the open
// dropdown flags, the column selectors, and the suggestion provider. Two
// canvases on one page get two instances and never see each other's rows,
// even when their graphs reuse node ids. The parts reach the instance
// through their props (canvasView passes it into every SlotContext and
// node), because props are all a gratify part sees at render, island, and
// gesture time.

import type { SuggestionList } from "@bimopenflow/contracts";
import type { CanvasIntent } from "./canvasIntents.js";
import type { IslandEntry } from "./canvasControls.js";
import { ColumnSelects } from "./columnSelect.js";
import type { LongValueEditor } from "./longValueEditor.js";

/** Live values for a suggest-annotated parameter (the host's suggestions endpoint). */
export type SuggestionProvider = (nodeId: string, param: string) => Promise<SuggestionList>;

/** State one mounted canvas owns; nothing here is shared between two canvases on a page. */
export interface CanvasInstance {
  /** Enters the runtime's intent flow from the DOM side (islands, long-value
   *  editors). The editor assigns it once the runtime exists; until then it
   *  drops intents. */
  dispatch: (intent: CanvasIntent) => void;
  /** A viewer: gestures never begin a mutation, islands are disabled, and
   *  the update function drops mutating intents. */
  readonly readOnly: boolean;
  /** The DOM document islands are created in; tests pass jsdom's. */
  readonly document: Document;
  /** One native input per parameter row, keyed by islandKey. */
  readonly islands: Map<string, IslandEntry>;
  /** Row keys whose canvas dropdown is open; while any is, native islands are detached. */
  readonly openDropdowns: Set<string>;
  /** One long-value editor per long-text row, and the theme version its textarea was styled for. */
  readonly longEditors: Map<string, LongValueEditor>;
  readonly longEditorThemeV: Map<string, number>;
  /** One editor per view.note node, keyed by node id. */
  readonly noteEditors: Map<string, LongValueEditor>;
  readonly noteEditorThemeV: Map<string, number>;
  readonly columnSelects: ColumnSelects;
  suggestionProvider: SuggestionProvider | null;
}

export interface CanvasInstanceOptions {
  readonly document: Document;
  readonly readOnly?: boolean;
  readonly suggestionProvider?: SuggestionProvider | null;
}

export function createCanvasInstance(options: CanvasInstanceOptions): CanvasInstance {
  const instance: CanvasInstance = {
    dispatch: () => {},
    readOnly: options.readOnly ?? false,
    document: options.document,
    islands: new Map(),
    openDropdowns: new Set(),
    longEditors: new Map(),
    longEditorThemeV: new Map(),
    noteEditors: new Map(),
    noteEditorThemeV: new Map(),
    columnSelects: new ColumnSelects(
      (nodeId, param) => instance.suggestionProvider
        ? instance.suggestionProvider(nodeId, param)
        : Promise.reject(new Error("No suggestion provider")),
      (nodeId, name, value) => instance.dispatch({ kind: "setParam", nodeId, name, value }),
      { readOnly: options.readOnly ?? false, document: options.document },
    ),
    suggestionProvider: options.suggestionProvider ?? null,
  };
  return instance;
}

/** Drops the islands, editors, and dropdown flags of parameter rows not in
 *  `liveKeys` (every row, when the set is empty), and the note editors of
 *  nodes not in `liveNoteIds`. */
export function pruneInstance(
  instance: CanvasInstance,
  liveKeys: ReadonlySet<string>,
  liveNoteIds: ReadonlySet<string> = new Set(),
): void {
  instance.columnSelects.prune(liveKeys);
  for (const key of instance.openDropdowns) if (!liveKeys.has(key)) instance.openDropdowns.delete(key);
  for (const [key, entry] of instance.islands) {
    if (!liveKeys.has(key)) {
      entry.detachSuggest?.();
      entry.el.remove();
      instance.islands.delete(key);
    }
  }
  pruneEditors(instance.longEditors, instance.longEditorThemeV, liveKeys);
  pruneEditors(instance.noteEditors, instance.noteEditorThemeV, liveNoteIds);
}

function pruneEditors(
  editors: Map<string, LongValueEditor>,
  themeVersions: Map<string, number>,
  live: ReadonlySet<string>,
): void {
  for (const [key, editor] of editors) {
    if (!live.has(key)) {
      editor.dispose();
      editors.delete(key);
      themeVersions.delete(key);
    }
  }
}
