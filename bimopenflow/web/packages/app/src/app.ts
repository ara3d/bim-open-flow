// The application controller: wires shell, store, canvas, sidebar, topbar,
// and pane area together around one ApiClient. Every graph mutation flows
// through store.dispatch; this module owns no graph logic.

import type { AnalysisSummary, NodeDescriptor } from "@bimopenflow/contracts";
import type { ApiClient } from "@bimopenflow/api-client";
import {
  connectAnalysis,
  createStore,
  serializeDocument,
  emptyDocument,
  type Action,
  type AnalysisConnection,
  type State,
} from "@bimopenflow/state";
import { buildShell } from "./shell.js";
import { createSidebar } from "./sidebar.js";
import { createTopbar } from "./topbar.js";
import { createPaneArea } from "./paneArea.js";
import { makePaneContext } from "./paneContext.js";
import { createCanvasEditor } from "./canvasEditor.js";
import { defaultPosition } from "./viewModel.js";
import { freshNodeId } from "./ids.js";
import { showToast } from "./toast.js";

export interface App {
  openAnalysis(id: string): Promise<void>;
  dispose(): void;
}

/** The last selected id that is an actual graph node (panes follow it). */
export function primaryNodeId(state: State): string | null {
  const nodeIds = new Set(state.document.structure.nodes.map((n) => n.id));
  for (let i = state.selection.length - 1; i >= 0; i--)
    if (nodeIds.has(state.selection[i]!)) return state.selection[i]!;
  return null;
}

export function createApp(root: HTMLElement, api: ApiClient): App {
  const store = createStore();
  const shell = buildShell(root);
  const catalog = new Map<string, NodeDescriptor>();

  let analyses: AnalysisSummary[] = [];
  let currentId: string | null = null;
  let connection: AnalysisConnection | null = null;

  const fail = (message: string) => showToast(message, "error");

  const dispatch = (action: Action) => {
    try {
      store.dispatch(action);
    } catch (e) {
      fail(e instanceof Error ? e.message : String(e));
    }
  };

  // ── panes ──────────────────────────────────────────────────────────────────
  // The pane area outlives analysis switches, so the context late-binds the
  // current analysis id on every request.
  const resultApi = { getResult: api.getResult.bind(api) };
  const boundCtx = {
    requestTable: (nodeId: string, port: string, skip?: number, take?: number) => {
      if (!currentId) return Promise.reject(new Error("No analysis open"));
      return makePaneContext(resultApi, currentId).requestTable(nodeId, port, skip, take);
    },
    resolveAsset: makePaneContext(resultApi, "").resolveAsset,
  };

  const paneArea = createPaneArea(shell.paneEl, {
    ctx: boundCtx,
    onSelect: (ids) => dispatch({ type: "select", ids }),
    onSetParam: (nodeId, name, value) =>
      dispatch({ type: "setParam", nodeId, name, value }),
    onError: fail,
  });

  // ── canvas ─────────────────────────────────────────────────────────────────
  const canvasEditor = createCanvasEditor(shell.canvas, store, () => catalog, fail);

  // ── chrome ─────────────────────────────────────────────────────────────────
  const topbar = createTopbar(shell.topbarEl, {
    onOpenAnalysis: (id) => void openAnalysis(id),
    onNewAnalysis: () => void newAnalysis(),
    onSave: () => void save(),
    onRun: () => void run(),
  });

  const sidebar = createSidebar(
    shell.sidebarEl,
    (id) => void openAnalysis(id),
    (desc) => addNode(desc),
  );

  // ── store -> UI ────────────────────────────────────────────────────────────
  let lastDoc = store.getState().document;
  let lastEval = store.getState().evalState;
  let lastPrimary: string | null = null;

  const shownFor = (state: State, nodeId: string) => ({
    nodeId,
    desc: catalog.get(
      state.document.structure.nodes.find((n) => n.id === nodeId)?.kind ?? "",
    ),
    values: { ...state.document.values[nodeId] },
    state: state.evalState[nodeId],
  });

  const unsubscribe = store.subscribe(() => {
    const state = store.getState();
    topbar.setDirty(state.dirty);
    const primary = primaryNodeId(state);
    const dataChanged = state.document !== lastDoc || state.evalState !== lastEval;
    if (primary === null) {
      if (lastPrimary !== null) paneArea.showNode(null);
    } else if (primary !== lastPrimary || dataChanged) {
      paneArea.showNode(shownFor(state, primary));
    } else {
      paneArea.updateSelection([...state.selection]);
    }
    lastDoc = state.document;
    lastEval = state.evalState;
    lastPrimary = primary;
  });

  paneArea.showNode(null);

  // ── actions ────────────────────────────────────────────────────────────────
  const refreshAnalyses = async () => {
    analyses = await api.listAnalyses();
    sidebar.setAnalyses(analyses, currentId);
    topbar.setAnalyses(analyses, currentId);
  };

  async function openAnalysis(id: string): Promise<void> {
    topbar.setConnection("connecting");
    connection?.dispose();
    connection = null;
    try {
      connection = await connectAnalysis(store, api, id);
      currentId = id;
      topbar.setConnection("connected");
      sidebar.setAnalyses(analyses, id);
      topbar.setAnalyses(analyses, id);
    } catch (e) {
      topbar.setConnection("offline");
      fail(`Could not open '${id}': ${e instanceof Error ? e.message : e}`);
    }
  }

  async function newAnalysis(): Promise<void> {
    const id = window.prompt("New analysis id:");
    if (!id) return;
    try {
      await api.putAnalysis(id, serializeDocument(emptyDocument));
      await refreshAnalyses();
      await openAnalysis(id);
    } catch (e) {
      fail(`Could not create '${id}': ${e instanceof Error ? e.message : e}`);
    }
  }

  async function save(): Promise<void> {
    if (!connection) return fail("No analysis open");
    try {
      await connection.save();
      showToast("Saved.");
    } catch (e) {
      fail(`Save failed: ${e instanceof Error ? e.message : e}`);
    }
  }

  async function run(): Promise<void> {
    if (!currentId) return fail("No analysis open");
    try {
      const summary = await api.createRun(currentId);
      showToast(`Run recorded: ${summary.fileName}`);
    } catch (e) {
      fail(`Run failed: ${e instanceof Error ? e.message : e}`);
    }
  }

  function addNode(desc: NodeDescriptor): void {
    if (!currentId) return fail("Open an analysis first");
    const state = store.getState();
    const id = freshNodeId(desc.kind, state.document.structure.nodes.map((n) => n.id));
    // TODO: fold add+place into one undo step once the state package offers a
    // compound action.
    dispatch({ type: "addNode", id, kind: desc.kind, version: desc.version });
    dispatch({
      type: "setLayout",
      nodeId: id,
      layout: defaultPosition(state.document.structure.nodes.length),
    });
    dispatch({ type: "select", ids: [id] });
  }

  // Undo/redo shortcuts (skipped while typing in a field).
  const onKeyDown = (e: KeyboardEvent) => {
    const target = e.target as HTMLElement | null;
    if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
      dispatch({ type: e.shiftKey ? "redo" : "undo" });
      e.preventDefault();
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "y") {
      dispatch({ type: "redo" });
      e.preventDefault();
    }
  };
  root.ownerDocument.addEventListener("keydown", onKeyDown);

  // ── boot ───────────────────────────────────────────────────────────────────
  void (async () => {
    try {
      await refreshAnalyses();
      const cat = await api.getNodeCatalog();
      for (const n of cat.nodes) catalog.set(n.kind, n);
      sidebar.setCatalog(cat.nodes);
      canvasEditor.refresh();
      topbar.setConnection("connected");
      if (analyses.length > 0) await openAnalysis(analyses[0]!.id);
    } catch {
      topbar.setConnection("offline");
      showToast("Host not reachable — start it and reload (see README).", "error");
    }
  })();

  return {
    openAnalysis,
    dispose() {
      unsubscribe();
      connection?.dispose();
      canvasEditor.dispose();
      paneArea.dispose();
      root.ownerDocument.removeEventListener("keydown", onKeyDown);
    },
  };
}
