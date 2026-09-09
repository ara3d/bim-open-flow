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
import { modelPathFor } from "./modelRef.js";
import { modelCatalog } from "./modelCatalog.js";
import { createCanvasEditor } from "./canvasEditor.js";
import { inlineParams } from "./canvasSlots.js";
import { setSuggestionProvider, refreshColumnOptions } from "./canvasControls.js";
import { autoLayout } from './autoLayout.js';
import { buildCanvasModel, freePosition, nodeHeight, nodeWidth } from "./viewModel.js";
import { freshNodeId, freshUntitledId } from "./ids.js";
import { loadThemeChoice, saveThemeChoice } from "./themeChoice.js";
import { showToast } from "./toast.js";
import { buildLiveViewRecipe } from "./liveViewRecipe";
import { nodeTitle, upstreamIds } from "./graphPreview";

export interface App {
  openAnalysis(id: string): Promise<void>;
  /** Re-reads the analysis list from the host (for graphs created outside the editor). */
  refreshAnalyses(): Promise<void>;
  dispose(): void;
}

/** Debounce for auto-saving document edits (which also re-evaluates server-side). */
export const AUTOSAVE_MS = 400;

/** The last selected id that is an actual graph node (panes follow it). */
export function primaryNodeId(state: State): string | null {
  const nodeIds = new Set(state.document.structure.nodes.map((n) => n.id));
  for (let i = state.selection.length - 1; i >= 0; i--)
    if (nodeIds.has(state.selection[i]!)) return state.selection[i]!;
  return null;
}

export interface AppOptions {
  tableOnly?: boolean;
  autoLayout?: boolean;
  graphDemo?: boolean;
  initialAnalysis?: string;
  /** Topbar heading; defaults to the BimOpenFlow / Snowdon 3D link. */
  heading?: string;
}

export function createApp(root: HTMLElement, api: ApiClient, options: AppOptions = {}): App {
  const store = createStore();
  const shell = buildShell(root, options.graphDemo);
  const catalog = new Map<string, NodeDescriptor>();

  let analyses: AnalysisSummary[] = [];
  let currentId: string | null = null;
  let connection: AnalysisConnection | null = null;
  let resultSelection: string[] = [];
  let lastPrimary: string | null = null;

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
  const resultApi = {
    getResult: api.getResult.bind(api),
    getSuggestions: api.getSuggestions.bind(api),
    getModelBosUrl: api.getModelBosUrl.bind(api),
  };
  const boundCtx = {
    requestTable: (nodeId: string, port: string, skip?: number, take?: number) => {
      if (!currentId) return Promise.reject(new Error("No flow open"));
      return makePaneContext(resultApi, currentId).requestTable(nodeId, port, skip, take);
    },
    requestSuggestions: (nodeId: string, param: string) => {
      if (!currentId) return Promise.reject(new Error("No flow open"));
      return makePaneContext(resultApi, currentId).requestSuggestions!(nodeId, param);
    },
    resolveAsset: makePaneContext(resultApi, "").resolveAsset,
  };
  setSuggestionProvider(boundCtx.requestSuggestions);

  // Model list for path -> catalog id resolution, fetched lazily and
  // re-fetched once on a miss (a model may have appeared since).
  const resolveModelId = modelCatalog(() => api.listModels());

  const paneArea = createPaneArea(shell.paneEl, {
    tableOnly: options.tableOnly,
    ctx: boundCtx,
    // Result object IDs are a different identity space from graph node IDs.
    onSelect: (ids) => { resultSelection = ids; paneArea.updateSelection(ids); },
    onSetParam: (nodeId, name, value) =>
      dispatch({ type: "setParam", nodeId, name, value }),
    onError: fail,
    resolveModelId,
  });

  // ── canvas ─────────────────────────────────────────────────────────────────
  const canvasEditor = createCanvasEditor(shell.canvas, store, () => catalog, fail, loadThemeChoice(), () => primaryNodeId(store.getState()) ?? lastPrimary);
  const preview = root.ownerDocument.createElement("select");
  preview.setAttribute("aria-label", "Preview node");
  preview.addEventListener("change", () => {
    dispatch({ type: "select", ids: [preview.value] });
    canvasEditor.focus(preview.value);
  });
  if (options.graphDemo) {
    const nodes = root.ownerDocument.createElement("button");
    nodes.textContent = "Nodes";
    nodes.setAttribute("aria-expanded", "false");
    nodes.addEventListener("click", () => {
      nodes.setAttribute("aria-expanded", String(root.classList.toggle("bof-app-catalog-open")));
    });
    const fit = root.ownerDocument.createElement("button");
    fit.textContent = "Fit graph";
    fit.addEventListener("click", () => canvasEditor.fit());
    const label = root.ownerDocument.createElement("label");
    label.append("Preview ", preview);
    const readable = root.ownerDocument.createElement("button");
    readable.textContent = "100%";
    readable.title = "Readable size: focus the preview node";
    readable.addEventListener("click", () => canvasEditor.focus(preview.value));
    shell.graphToolbar.append(nodes, fit, readable, label);
  }

  // ── chrome ─────────────────────────────────────────────────────────────────
  const topbar = createTopbar(shell.topbarEl, {
    heading: options.heading,
    onOpenAnalysis: (id) => void openAnalysis(id),
    onNewAnalysis: () => void newAnalysis(),
    onSave: () => void save(),
    onRun: () => void run(),
    onThemeChange: (name) => {
      saveThemeChoice(name);
      canvasEditor.setTheme(name);
    },
  });
  topbar.setTheme(loadThemeChoice());

  const sidebar = createSidebar(
    shell.sidebarEl,
    (id) => void openAnalysis(id),
    (desc) => addNode(desc),
  );

  // ── store -> UI ────────────────────────────────────────────────────────────
  let lastDoc = store.getState().document;
  let lastEval = store.getState().evalState;
  let lastDirty = false;

  const shownFor = (state: State, nodeId: string) => ({
    nodeId,
    desc: catalog.get(
      state.document.structure.nodes.find((n) => n.id === nodeId)?.kind ?? "",
    ),
    values: { ...state.document.values[nodeId] },
    state: state.evalState[nodeId],
    modelPath: modelPathFor(state.document, nodeId),
    pending: state.dirty,
    live: buildLiveViewRecipe(state.document,nodeId,catalog),
    lineage: [...upstreamIds(state.document,nodeId)].reverse().map(id => {
      const node = state.document.structure.nodes.find(n=>n.id===id);
      return node ? nodeTitle(node.kind) : id;
    }).join(" → "),
  });

  const unsubscribe = store.subscribe(() => {
    const state = store.getState();
    topbar.setDirty(state.dirty);
    if (state.evalState !== lastEval) refreshColumnOptions();
    // Keep the last preview visible while panning or clearing graph selection.
    const primary = primaryNodeId(state) ?? (options.graphDemo &&
      state.document.structure.nodes.some(n => n.id === lastPrimary) ? lastPrimary : null);
    const dataChanged = state.document !== lastDoc || state.evalState !== lastEval || state.dirty !== lastDirty;
    if (options.graphDemo && state.document !== lastDoc) {
      preview.replaceChildren(...state.document.structure.nodes.map(n => {
        const option = root.ownerDocument.createElement("option");
        option.value = n.id;
        option.textContent = `${nodeTitle(n.kind)} · ${n.id}`;
        return option;
      }));
    }
    preview.value = primary ?? "";
    if (primary === null) {
      if (lastPrimary !== null) paneArea.showNode(null);
    } else if (primary !== lastPrimary || dataChanged) {
      paneArea.showNode(shownFor(state, primary));
    } else {
      paneArea.updateSelection(resultSelection);
    }
    lastDoc = state.document;
    lastEval = state.evalState;
    lastPrimary = primary;
    lastDirty = state.dirty;
  });

  paneArea.showNode(null);

  // ── actions ────────────────────────────────────────────────────────────────
  const refreshAnalyses = async () => {
    analyses = await api.listAnalyses();
    sidebar.setAnalyses(analyses, currentId);
    topbar.setAnalyses(analyses, currentId);
  };

  async function openAnalysis(id: string): Promise<void> {
    resultSelection = [];
    lastPrimary = null;
    paneArea.showNode(null);
    topbar.setConnection("connecting");
    connection?.dispose();
    connection = null;
    try {
      connection = await connectAnalysis(store, api, id, {
        autosaveMs: AUTOSAVE_MS,
        onSaveError: (e) => fail(`Autosave failed: ${e instanceof Error ? e.message : e}`),
      });
      currentId = id;
      refreshColumnOptions();
      topbar.setConnection("connected");
      sidebar.setAnalyses(analyses, id);
      topbar.setAnalyses(analyses, id);
      if (options.graphDemo) {
        if (options.autoLayout) {
          const positions = autoLayout(buildCanvasModel(store.getState(), catalog), { width: shell.canvas.clientWidth, height: shell.canvas.clientHeight });
          for (const [nodeId, layout] of Object.entries(positions)) dispatch({ type: 'setLayout', nodeId, layout });
        }
        const nodes = store.getState().document.structure.nodes;
        const initial = nodes.find(n => n.kind === "view3d.categoryStyle") ??
          nodes.find(n => n.kind.startsWith("view3d.")) ?? nodes[0];
        if (initial) dispatch({ type: "select", ids: [initial.id] });
        if (options.autoLayout) canvasEditor.fit();
        else canvasEditor.focus(initial?.id);
      }
    } catch (e) {
      topbar.setConnection("offline");
      fail(`Could not open flow '${id}': ${e instanceof Error ? e.message : e}`);
    }
  }

  // No prompt: embedded browsers throw on window.prompt. The id is the next
  // free untitled-N. TODO: no rename UI exists yet, so the name sticks.
  async function newAnalysis(): Promise<void> {
    const id = freshUntitledId(analyses.map((a) => a.id));
    try {
      await api.putAnalysis(id, serializeDocument(emptyDocument));
      await refreshAnalyses();
      await openAnalysis(id);
    } catch (e) {
      fail(`Could not create '${id}': ${e instanceof Error ? e.message : e}`);
    }
  }

  async function save(): Promise<void> {
    if (!connection) return fail("No flow open");
    try {
      await connection.save();
      showToast("Saved.");
    } catch (e) {
      fail(`Save failed: ${e instanceof Error ? e.message : e}`);
    }
  }

  async function run(): Promise<void> {
    if (!currentId) return fail("No flow open");
    try {
      const summary = await api.createRun(currentId);
      showToast(`Run recorded: ${summary.fileName}`);
    } catch (e) {
      fail(`Run failed: ${e instanceof Error ? e.message : e}`);
    }
  }

  function addNode(desc: NodeDescriptor): void {
    if (!currentId) return fail("Open a flow first");
    const state = store.getState();
    const id = freshNodeId(desc.kind, state.document.structure.nodes.map((n) => n.id));
    // Size-aware placement: the first grid spot where this node's real
    // width/height (inline param slots included) overlaps nothing.
    const params = inlineParams(desc.params, {});
    const position = freePosition(
      buildCanvasModel(state, catalog).nodes,
      nodeWidth(params),
      nodeHeight(desc.inputs.length, desc.outputs.length, params),
    );
    // TODO: fold add+place into one undo step once the state package offers a
    // compound action.
    dispatch({ type: "addNode", id, kind: desc.kind, version: desc.version });
    dispatch({ type: "setLayout", nodeId: id, layout: position });
    dispatch({ type: "select", ids: [id] });
    root.classList.remove("bof-app-catalog-open");
    shell.graphToolbar.querySelector("button")?.setAttribute("aria-expanded", "false");
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
      const [, cat] = await Promise.all([refreshAnalyses(), api.getNodeCatalog()]);
      for (const n of cat.nodes) catalog.set(n.kind, n);
      if (options.graphDemo && catalog.has("view3d.section") && catalog.get("view3d.section")?.params.find(p => p.name === "fraction")?.control?.kind !== "slider")
        fail("The 3D backend is out of date. Rebuild and restart BimOpenFlow.Host, then reload this page to enable the node controls.");
      sidebar.setCatalog(cat.nodes);
      canvasEditor.refresh();
      topbar.setConnection("connected");
      // Always land in an open analysis so no click can fail for lack of one;
      // if the stored analysis fails to open, fall back to a fresh one.
      if (options.initialAnalysis) {
        if (!analyses.some(a => a.id === options.initialAnalysis)) {
          fail(`Snowdon graph is missing. Start the BIM-profile host with a Snowdon model and an empty store, or import snowdon-toolkit.json (see docs/bim-flow-3d.md).`);
          return;
        }
        await openAnalysis(options.initialAnalysis);
      } else if (analyses.length > 0) await openAnalysis(analyses[0]!.id);
      if (currentId === null) await newAnalysis();
    } catch {
      topbar.setConnection("offline");
      showToast("Host not reachable — start it and reload (see README).", "error");
    }
  })();

  return {
    openAnalysis,
    refreshAnalyses,
    dispose() {
      unsubscribe();
      setSuggestionProvider(null);
      connection?.dispose();
      canvasEditor.dispose();
      paneArea.dispose();
      shell.dispose();
      root.ownerDocument.removeEventListener("keydown", onKeyDown);
    },
  };
}
