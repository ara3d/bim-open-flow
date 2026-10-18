import type {
  EntityProperties,
  NodeDescriptor,
  NodeState,
  SelectionEvent,
  SuggestionList,
  TableSlice,
} from "@bimopenflow/contracts";

/** Model file formats the 3D pane can load. */
export type ModelFormat = "bos" | "bfast" | "glb";

/**
 * A node a pane asks the editor to add. `inputs` wires one of its input ports
 * to an output of a node planned in the same call ({ node: index, port }) or of
 * an existing node ({ nodeId, port }). Params are canonical text; a List param
 * is comma-separated.
 */
export interface PlannedNode {
  kind: string;
  params?: Record<string, string>;
  inputs?: Record<string, { node: number; port: string } | { nodeId: string; port: string }>;
}

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
  /**
   * Properties of one entity of a loaded model. modelUrl is the same URL the
   * pane was given (e.g. "model:duplex.bos"); localId is the entity key a pick
   * reports. Optional: a pane without it simply shows no property panel.
   */
  requestEntityProperties?(
    modelUrl: string,
    localId: number,
  ): Promise<EntityProperties>;
  /**
   * Adds the nodes, placed free of overlap, wired as planned, the last one
   * selected, as one undo step. Resolves to their ids in order. Optional: a
   * pane without it offers no graph-editing control.
   */
  addNodes?(nodes: readonly PlannedNode[]): Promise<string[]>;
  /**
   * Sets params on an existing node as one undo step; rejects when the node is
   * not in the open graph, so the pane can add a new one instead.
   */
  setParams?(nodeId: string, params: Record<string, string>): Promise<void>;
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
  | {
      kind: "model";
      url: string;
      format?: ModelFormat;
      /** The graph's own path for the model (a value.path or a node's path param), for nodes that take a file path. */
      path?: string;
    }
  /** A 3D instance table: per-instance colors and isolation (3D pane). */
  | { kind: "instances"; data: TableSlice }
  /** A bounded, ordered visualization recipe from view3d.scene and its downstream nodes. */
  | { kind: "view"; data: TableSlice }
  /** A boxes table: axis-aligned boxes rendered as instanced unit cubes (3D pane). */
  | { kind: "boxes"; data: TableSlice }
  /** A legend table (view.colormap, view3d.color, chart.bar `legend` port); 3D and chart panes render it. */
  | { kind: "legend"; data: TableSlice };

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
