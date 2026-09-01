import type {
  NodeDescriptor,
  NodeState,
  SelectionEvent,
  SuggestionList,
  TableSlice,
} from "@bimopenflow/contracts";

/** Model file formats the 3D pane can load. */
export type ModelFormat = "bos" | "glb";

/**
 * Host services a pane may call. Provided once at mount; panes hold no other
 * reference to the application.
 */
export interface PaneContext {
  /** Fetches a slice of a node output table (paged via skip/take). */
  requestTable(
    nodeId: string,
    port: string,
    skip?: number,
    take?: number,
  ): Promise<TableSlice>;
  /** Maps a model/asset URL from graph data to a fetchable URL. */
  resolveAsset(url: string): string;
  /** Live value suggestions for a suggest-annotated node parameter. */
  requestSuggestions?(nodeId: string, param: string): Promise<SuggestionList>;
}

/**
 * Data pushed into a pane. Small, explicit, and JSON-serializable; panes
 * ignore kinds they do not handle.
 */
export type PaneInput =
  /** A table to display (table, chart, and verdict panes). */
  | { kind: "table"; data: TableSlice }
  /** Evaluation state of a node (inspector pane refreshes its status section). */
  | { kind: "nodeState"; state: NodeState }
  /** The application selection, for panes that mirror/highlight it. */
  | { kind: "selection"; ids: string[] }
  /** Node metadata + current param values (inspector pane). */
  | {
      kind: "inspect";
      node: NodeDescriptor;
      values: Record<string, string>;
      state?: NodeState;
      /** Graph node id, for panes that call back per node (e.g. suggestions). */
      nodeId?: string;
    }
  /** A model to load into the 3D pane; format inferred from the URL when omitted. */
  | { kind: "model"; url: string; format?: ModelFormat }
  /** A 3D instance table: per-instance colors and isolation (3D pane). */
  | { kind: "instances"; data: TableSlice };

/** Events a pane emits. Serializable; the host decides what they mean. */
export type PaneEvent =
  /** The user selected something inside the pane. */
  | { kind: "selection"; event: SelectionEvent }
  /** A pane-specific notification (e.g. modelLoaded, loadError). */
  | { kind: "action"; action: string; payload?: Record<string, string> };

/**
 * The single pane contract: every pane implements exactly this interface.
 * Lifecycle: mount once, update any number of times, destroy once
 * (idempotent). update before mount throws. onEvent may be called at any
 * time and supports multiple handlers.
 */
export interface Pane {
  mount(el: HTMLElement, ctx: PaneContext): void;
  update(input: PaneInput): void;
  onEvent(handler: (e: PaneEvent) => void): void;
  destroy(): void;
}
