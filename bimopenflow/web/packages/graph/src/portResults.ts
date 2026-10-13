// The port-results controller: row counts and the one open peek, driven by
// the store's evaluation state. Gratify-free and DOM-free (design.md,
// "portResults.ts: the controller") so it is testable headless and reusable
// by any surface that needs port results.

import type { NodeDescriptor, PortSchema, TableSlice } from "@bimopenflow/contracts";
import { parsePortRef, schemaOf, type State, type Store } from "@bimopenflow/state";

/** One page of a node output from the host; PaneContext.requestTable's shape. */
export type ReadPort = (nodeId: string, port: string, skip: number, take: number) => Promise<TableSlice>;

/** Rows a peek card shows and requests. */
export const PEEK_ROWS = 5;
/** Count requests in flight at once; peek requests bypass this queue. */
export const COUNT_CONCURRENCY = 2;

/** A wire's row count and, when the count's slice carried a schema, its column
 *  count; `current` is false while a newer evaluation's count is in flight. */
export interface WireRows { readonly rows: number; readonly current: boolean; readonly columns?: number }

export type PortPeek =
  | { readonly kind: "loading" }
  /** `inferred` is the static schema pass's answer for the same endpoint, kept so
   *  the card can flag recorded columns that differ from it. The data still wins. */
  | { readonly kind: "ready"; readonly slice: TableSlice; readonly inferred?: PortSchema }
  /** No result yet, but the static schema pass (TKT-209) knows something about the
   *  columns. `reason` says why there is no result, as `absent` would. */
  | { readonly kind: "inferred"; readonly schema: PortSchema; readonly reason: string }
  | { readonly kind: "absent"; readonly reason: string };

export interface PortPeekView {
  readonly endpoint: string; // "nodeId.port"
  readonly pinned: boolean;
  readonly peek: PortPeek;
  /** The schema entering the node on its first table input, when the static
   *  pass knows it (set by buildCanvasModel): the card names the columns the
   *  step added or dropped against it, which is what a step did to the table. */
  readonly feeding?: PortSchema;
}

/** Immutable snapshot for buildCanvasModel; a new object after every change. */
export interface PortResultsView {
  readonly counts: ReadonlyMap<string, WireRows>; // keyed by source endpoint
  readonly peek: PortPeekView | null;
}

export const NO_PORT_RESULTS: PortResultsView = { counts: new Map(), peek: null };

/** Distinct source endpoints, in edge order, of wires whose source port is Table or
 *  Relation and whose source node's status is Ok. */
export function countTargets(state: State, catalog: ReadonlyMap<string, NodeDescriptor>): string[] {
  const targets: string[] = [];
  const seen = new Set<string>();
  for (const edge of state.document.structure.edges) {
    const endpoint = edge.from;
    if (seen.has(endpoint)) continue;
    const { nodeId, port } = parsePortRef(endpoint);
    if (state.evalState[nodeId]?.status !== "Ok") continue;
    const node = state.document.structure.nodes.find((n) => n.id === nodeId);
    if (!node) continue;
    const portType = catalog.get(node.kind)?.outputs.find((p) => p.name === port)?.type;
    if (portType !== "Table" && portType !== "Relation") continue;
    seen.add(endpoint);
    targets.push(endpoint);
  }
  return targets;
}

/** Why the host has nothing for this endpoint, or null when its node is Ok. */
export function absentReason(state: State, endpoint: string): string | null {
  const { nodeId } = parsePortRef(endpoint);
  const nodeState = state.evalState[nodeId];
  if (!nodeState) return "Not evaluated yet";
  if (nodeState.status === "Ok") return null;
  if (nodeState.status === "EffectPending") return "Writes on Run; no rows until the graph runs";
  return nodeState.error ?? nodeState.status;
}

function reasonMessage(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

export interface PortResults {
  view(): PortResultsView;
  /** New evaluation: bump the generation, mark counts not current, drop counts of
   *  endpoints no longer targeted, re-count the targets, refetch the open peek. */
  evaluated(state: State, catalog: ReadonlyMap<string, NodeDescriptor>): void;
  /** Hovered endpoint or null; ignored while a card is pinned. */
  hover(endpoint: string | null, state: State): void;
  /** Pin the card to an endpoint; null unpins and closes it. */
  pin(endpoint: string | null, state: State): void;
  dispose(): void;
}

export function createPortResults(read: ReadPort, onChange: () => void): PortResults {
  let disposed = false;
  let generation = 0;
  let counts = new Map<string, WireRows>();

  let openEndpoint: string | null = null;
  let pinned = false;
  // Resolved peek results for the current generation, keyed by endpoint.
  // Cleared on every evaluation, so a stale peek is never shown across passes.
  let peekCache = new Map<string, PortPeek>();
  const peekInFlight = new Set<string>();

  let activeCounts = 0;
  const countQueue: (() => void)[] = [];

  function notify(): void {
    if (!disposed) onChange();
  }

  function view(): PortResultsView {
    const peek =
      openEndpoint === null
        ? null
        : { endpoint: openEndpoint, pinned, peek: peekCache.get(openEndpoint) ?? { kind: "loading" as const } };
    return { counts, peek };
  }

  function pumpCountQueue(): void {
    while (activeCounts < COUNT_CONCURRENCY && countQueue.length > 0) {
      const job = countQueue.shift()!;
      activeCounts++;
      job();
    }
  }

  function enqueueCount(endpoint: string, gen: number): void {
    countQueue.push(() => {
      const { nodeId, port } = parsePortRef(endpoint);
      read(nodeId, port, 0, 0)
        .then((slice) => {
          if (gen !== generation || disposed) return;
          counts = new Map(counts);
          counts.set(endpoint, { rows: slice.totalRows, current: true, ...(slice.columns.length > 0 ? { columns: slice.columns.length } : {}) });
          notify();
        })
        .catch(() => {
          // Leave the endpoint's count as not-current; there is no card to
          // show a count error on, unlike a peek.
        })
        .finally(() => {
          activeCounts--;
          pumpCountQueue();
        });
    });
  }

  /** What a card shows when there is no result: the inferred schema when the pass
   *  has one for the endpoint, else the bare reason. */
  function withoutResult(state: State, endpoint: string, reason: string): PortPeek {
    const schema = schemaOf(state, endpoint);
    return schema ? { kind: "inferred", schema, reason } : { kind: "absent", reason };
  }

  function startPeekIfNeeded(endpoint: string, state: State): void {
    if (peekCache.has(endpoint) || peekInFlight.has(endpoint)) return;
    const reason = absentReason(state, endpoint);
    if (reason !== null) {
      peekCache.set(endpoint, withoutResult(state, endpoint, reason));
      notify();
      return;
    }
    const gen = generation;
    peekInFlight.add(endpoint);
    const { nodeId, port } = parsePortRef(endpoint);
    read(nodeId, port, 0, PEEK_ROWS).then(
      (slice) => {
        peekInFlight.delete(endpoint);
        if (gen !== generation || disposed) return;
        const inferred = schemaOf(state, endpoint);
        peekCache.set(endpoint, { kind: "ready", slice, ...(inferred ? { inferred } : {}) });
        notify();
      },
      (err) => {
        peekInFlight.delete(endpoint);
        if (gen !== generation || disposed) return;
        peekCache.set(endpoint, withoutResult(state, endpoint, reasonMessage(err)));
        notify();
      },
    );
  }

  return {
    view,

    evaluated(state, catalog) {
      generation++;
      const gen = generation;
      // Drop queued (not yet started) count jobs: they were built for a
      // generation that just went stale.
      countQueue.length = 0;
      peekCache = new Map();
      peekInFlight.clear();

      const targets = countTargets(state, catalog);
      const targetSet = new Set(targets);
      const nextCounts = new Map<string, WireRows>();
      for (const [endpoint, wireRows] of counts)
        if (targetSet.has(endpoint)) nextCounts.set(endpoint, { ...wireRows, current: false });
      counts = nextCounts;
      for (const endpoint of targets) enqueueCount(endpoint, gen);
      notify();
      pumpCountQueue();

      if (openEndpoint !== null) startPeekIfNeeded(openEndpoint, state);
    },

    hover(endpoint, state) {
      if (pinned) return;
      openEndpoint = endpoint;
      notify();
      if (endpoint !== null) startPeekIfNeeded(endpoint, state);
    },

    pin(endpoint, state) {
      if (endpoint === null) {
        pinned = false;
        openEndpoint = null;
        notify();
        return;
      }
      pinned = true;
      openEndpoint = endpoint;
      notify();
      startPeekIfNeeded(endpoint, state);
    },

    dispose() {
      disposed = true;
    },
  };
}

/** Calls results.evaluated whenever the store's evalState changes identity. Returns unsubscribe. */
export function watchEvaluations(
  store: Store,
  getCatalog: () => ReadonlyMap<string, NodeDescriptor>,
  results: PortResults,
): () => void {
  let lastEvalState = store.getState().evalState;
  return store.subscribe(() => {
    const state = store.getState();
    if (state.evalState === lastEvalState) return;
    lastEvalState = state.evalState;
    results.evaluated(state, getCatalog());
  });
}
