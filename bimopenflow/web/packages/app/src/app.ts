// The application controller: wires shell, store, canvas, sidebar, topbar,
// and pane area together around one ApiClient. Every graph mutation flows
// through store.dispatch; this module owns no graph logic.

import type { AnalysisSummary, EditorSession, NodeDescriptor } from "@bimopenflow/contracts";
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
import { createGraphEditor } from "@bimopenflow/graph";
import { autoLayout } from "@bimopenflow/graph";
import { buildCanvasModel } from "@bimopenflow/graph";
import { freshCopyId, freshUntitledId } from "./ids.js";
import { loadThemeChoice, saveThemeChoice } from "./themeChoice.js";
import { setNodeStyle } from "@bimopenflow/graph";
import { loadNodeStyleChoice, saveNodeStyleChoice } from "./nodeStyleChoice.js";
import { installCanvasPalette, paletteClientPoint, paletteKeyOpens } from "./canvasPalette.js";
import { addNodeActions, freeSpot } from "./addNodePlan.js";
import { createStepList, stepListModel } from "./stepList.js";
import { createProblemsPanel } from "./problemsPanel.js";
import { graphProblems } from "./graphProblems.js";
import { createStartPage } from "./startPage.js";
import { TEMPLATES } from "./templates.generated.js";
import { chooseInitialAnalysis, searchWithAnalysis } from "./analysisParam.js";
import { showToast } from "./toast.js";
import { buildLiveViewRecipe } from "./liveViewRecipe";
import { createHostStatus, type HostStatusSource } from "./hostStatus.js";
import { mountAskPanel, type AskPanel } from "./askPanel.js";
import { nodeTitle, upstreamIds } from "@bimopenflow/graph";
import { primaryNodeId, reopenKeepingSelection, selectedNodeIds } from "./selection.js";
import { createSessionReporter } from "./editorSession.js";
import {
  backToAnswer,
  initialShownChoice,
  resolveShown,
  showOverride,
  togglePin,
  type ShownChoice,
} from "./shownChoice.js";

export interface App {
  openAnalysis(id: string): Promise<void>;
  /** Re-reads the analysis list from the host (for graphs created outside the editor). */
  refreshAnalyses(): Promise<void>;
  /** The session last reported (or queued) to the host. */
  session(): EditorSession;
  dispose(): void;
}

/** Debounce for auto-saving document edits (which also re-evaluates server-side). */
export const AUTOSAVE_MS = 400;

export { primaryNodeId } from "./selection.js";

export interface AppOptions {
  tableOnly?: boolean;
  autoLayout?: boolean;
  graphDemo?: boolean;
  initialAnalysis?: string;
  /** Keep `?analysis=<id>` in the address bar in step with the open analysis
   *  (main editor page only; other query parameters are kept). */
  syncUrl?: boolean;
  /** Topbar heading; defaults to the BimOpenFlow / Snowdon 3D link. */
  heading?: string;
  /** The host status the api reports into (see watchHost). Without one the
   *  app only learns about the host from its own periodic probe. */
  host?: HostStatusSource;
}

export function createApp(root: HTMLElement, api: ApiClient, options: AppOptions = {}): App {
  const store = createStore();
  const shell = buildShell(root, options.graphDemo);
  const catalog = new Map<string, NodeDescriptor>();

  let analyses: AnalysisSummary[] = [];
  let currentId: string | null = null;
  let connection: AnalysisConnection | null = null;
  /** The open in progress; a newer open aborts it. */
  let opening: AbortController | null = null;
  let resultSelection: string[] = [];
  /** The node id currently shown in the pane area (TKT-81: the answer, an
   *  override, or a pin's target — never just "the selection"). */
  let lastPrimary: string | null = null;
  let shownChoice: ShownChoice = initialShownChoice();
  let currentSession: EditorSession = { analysisId: undefined, selection: [] };

  const fail = (message: string) => showToast(message, "error");
  const host = options.host ?? createHostStatus();
  if (!options.host) host.start(() => api.listModels());

  // Reports which analysis is open and which nodes are selected, so a
  // separate process (the stdio MCP server) reading the same store can see
  // what the studio shows. See Design: "The editor keeps the record current".
  const sessionReporter = createSessionReporter(api, {
    onError: (e) => fail(`Could not report the open session: ${e instanceof Error ? e.message : e}`),
  });
  const reportSession = () => {
    currentSession = { analysisId: currentId ?? undefined, selection: selectedNodeIds(store.getState()) };
    sessionReporter.report(currentSession);
  };

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
    getEntityProperties: api.getEntityProperties.bind(api),
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
    requestEntityProperties: makePaneContext(resultApi, "").requestEntityProperties,
  };

  // Model list for path -> catalog id resolution, fetched lazily and
  // re-fetched once on a miss (a model may have appeared since).
  const resolveModelId = modelCatalog(() => api.listModels());

  const paneArea = createPaneArea(shell.paneEl, {
    tableOnly: options.tableOnly,
    ctx: boundCtx,
    // Result object IDs are a different identity space from graph node IDs.
    onSelect: (ids) => { resultSelection = ids; paneArea.updateSelection(ids); },
    onError: fail,
    resolveModelId,
    onTogglePin: () => togglePaneAnswerPin(),
    onShowAnswer: () => showAnswerInPane(),
  });

  // ── canvas ─────────────────────────────────────────────────────────────────
  // The palette (TKT-96): right-click on empty canvas, a wire dropped there,
  // or Space (TKT-120) offers kinds; a pick adds, places, selects, and wires
  // the node as one undo step through the batch action. Space has no world
  // point (the graph package keeps its client-to-world conversion private),
  // so its pick lands on the first free spot, as the Nodes tab's add does.
  const palette = installCanvasPalette(shell.canvas, {
    getCatalog: () => [...catalog.values()],
    onPick: (entry, at, wire) => {
      if (!currentId) return fail("Open a flow first");
      const state = store.getState();
      const actions = addNodeActions(state, entry.desc, at ?? freeSpot(state, catalog, entry.desc),
        wire && entry.port ? { from: wire, port: entry.port } : undefined);
      dispatch({ type: "batch", actions });
    },
  });
  setNodeStyle(loadNodeStyleChoice());
  const canvasEditor = createGraphEditor(shell.canvas, {
    store,
    catalog: () => catalog,
    onError: fail,
    theme: loadThemeChoice(),
    getPreview: () => primaryNodeId(store.getState()) ?? lastPrimary,
    onShowNode: (nodeId) => showNodeInPane(nodeId),
    readPort: boundCtx.requestTable, // TKT-11: peek cards and row counts
    suggestions: boundCtx.requestSuggestions,
    surfaces: {
      onEmptyCanvas: (client, world) => palette.open(client, world),
      onWireDropped: (from, world, client) => {
        const node = store.getState().document.structure.nodes.find((n) => n.id === from.nodeId);
        const desc = node && catalog.get(node.kind);
        const type = (from.dir === "out" ? desc?.outputs : desc?.inputs)?.find((p) => p.name === from.port)?.type;
        if (type) palette.open(client, world, { from, type });
      },
      onResultsChange: () => renderGraphViews(store.getState()),
    },
  });
  // Selecting from the step list or the problems strip also brings the node
  // into view; a click on the canvas itself never moves the viewport.
  const selectAndFocus = (nodeId: string) => {
    dispatch({ type: "select", ids: [nodeId] });
    canvasEditor.focus(nodeId);
  };
  const problems = createProblemsPanel(shell.canvasHost, { onSelect: selectAndFocus });
  const preview = root.ownerDocument.createElement("select");
  preview.setAttribute("aria-label", "Preview node");
  // TKT-113: choosing in the picker is an explicit choice, like a
  // double-click, so the pane shows the chosen node (or returns to the answer
  // when the answer is chosen); the node is also selected and brought into view.
  preview.addEventListener("change", () => {
    const nodeId = preview.value;
    showPickedInPane(nodeId);
    dispatch({ type: "select", ids: [nodeId] });
    canvasEditor.focus(nodeId);
  });
  if (options.graphDemo) {
    const nodes = root.ownerDocument.createElement("button");
    nodes.textContent = "Nodes";
    nodes.setAttribute("aria-expanded", "false");
    nodes.addEventListener("click", () => {
      const open = root.classList.toggle("bof-app-catalog-open");
      nodes.setAttribute("aria-expanded", String(open));
      if (open) sidebar.showTab("nodes");
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
    // "New" opens the start page (TKT-14); its Blank card creates an empty flow.
    onNewAnalysis: () => startPage.show(),
    onSave: () => void save(),
    onRun: () => void run(),
    onThemeChange: (name) => {
      saveThemeChoice(name);
      canvasEditor.setTheme(name);
    },
    onNodeStyleChange: (name) => {
      saveNodeStyleChoice(name);
      setNodeStyle(name);
      canvasEditor.refresh();
    },
  });
  topbar.setTheme(loadThemeChoice());
  topbar.setNodeStyle(loadNodeStyleChoice());

  const sidebar = createSidebar(shell.sidebarEl, (desc) => addNode(desc));
  const stepList = createStepList(sidebar.stepsEl, { onSelect: selectAndFocus });

  // The start page (TKT-14): one card per sample flow, over the canvas. Shown
  // at boot in the plain editor, and from the topbar's New button.
  const startPage = createStartPage(shell.canvasHost, {
    templates: TEMPLATES,
    onOpen: (id) => { startPage.hide(); void openAnalysis(id); },
    onCopy: (id) => { startPage.hide(); void copyAnalysis(id); },
    onBlank: () => { startPage.hide(); void newAnalysis(); },
  });

  /** The two whole-graph readings (TKT-95, TKT-97), re-rendered on every
   *  store change and whenever a row count arrives. */
  const renderGraphViews = (state: State) => {
    stepList.render(stepListModel(state, catalog, canvasEditor.results()));
    problems.render(graphProblems(state));
  };

  // ── store -> UI ────────────────────────────────────────────────────────────
  let lastDoc = store.getState().document;
  let lastEval = store.getState().evalState;
  let lastDirty = false;
  let lastSelection = store.getState().selection;

  const shownFor = (state: State, nodeId: string, isAnswer: boolean) => ({
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
    default: isAnswer,
    pinned: shownChoice.pinned,
  });

  // TKT-81: the pane follows the flow's answer node and stays there while the
  // user selects nodes or edits parameters — selection never drives what the
  // pane shows. showNodeInPane/showAnswerInPane/showPickedInPane/
  // togglePaneAnswerPin (below) are the only way to change that, each
  // re-running this from outside a store update (a pin/show/back click
  // touches no store state).
  function applyShown(state: State, dataChanged: boolean): void {
    const { id: shownId, isAnswer } = resolveShown(state.document, state.evalState, catalog, shownChoice);
    preview.value = shownId ?? "";
    if (shownId === null) {
      if (lastPrimary !== null) paneArea.showNode(null);
    } else if (shownId !== lastPrimary || dataChanged) {
      paneArea.showNode(shownFor(state, shownId, isAnswer));
    } else {
      paneArea.updateSelection(resultSelection);
    }
    lastPrimary = shownId;
  }

  /** Shows `nodeId` explicitly (double-click on the canvas), replacing the answer. */
  function showNodeInPane(nodeId: string): void {
    showOverride(shownChoice, nodeId);
    applyShown(store.getState(), true);
  }

  /** The Preview node picker: the answer goes back to following it, any
   *  other node is shown as an override, exactly as a double-click shows it. */
  function showPickedInPane(nodeId: string): void {
    if (nodeId === shownChoice.lastAnswer) showAnswerInPane();
    else showNodeInPane(nodeId);
  }

  /** The pane header's "Back to answer" button: drops the override and any pin. */
  function showAnswerInPane(): void {
    backToAnswer(shownChoice);
    applyShown(store.getState(), true);
  }

  /** The pane header's pin toggle: freezes on whatever the pane shows now. */
  function togglePaneAnswerPin(): void {
    togglePin(shownChoice, lastPrimary);
    applyShown(store.getState(), true);
  }

  const unsubscribe = store.subscribe(() => {
    const state = store.getState();
    topbar.setDirty(state.dirty);
    // Deferred one microtask so it runs after canvasEditor's own subscriber
    // (registered first, above) has pruned the previous document's column
    // selects; otherwise a flow switch re-requests suggestions for the old
    // flow's node ids against the new analysis id and the host 404s.
    if (state.evalState !== lastEval) queueMicrotask(() => canvasEditor.refreshSuggestions());
    const dataChanged = state.document !== lastDoc || state.evalState !== lastEval || state.dirty !== lastDirty;
    if (options.graphDemo && state.document !== lastDoc) {
      preview.replaceChildren(...state.document.structure.nodes.map(n => {
        const option = root.ownerDocument.createElement("option");
        option.value = n.id;
        option.textContent = `${nodeTitle(n.kind)} · ${n.id}`;
        return option;
      }));
    }
    applyShown(state, dataChanged);
    if (dataChanged || state.selection !== lastSelection) renderGraphViews(state);
    lastSelection = state.selection;
    lastDoc = state.document;
    lastEval = state.evalState;
    lastDirty = state.dirty;
    // Report whenever the selected nodes or the open analysis changed
    // (openAnalysis itself reports too, for the case the store does not fire).
    const selection = selectedNodeIds(state);
    const sessionChanged = currentSession.analysisId !== (currentId ?? undefined) ||
      selection.length !== currentSession.selection.length ||
      selection.some((id, i) => id !== currentSession.selection[i]);
    if (sessionChanged) reportSession();
  });

  paneArea.showNode(null);

  // ── actions ────────────────────────────────────────────────────────────────
  const refreshAnalyses = async () => {
    analyses = await api.listAnalyses();
    topbar.setAnalyses(analyses, currentId);
    startPage.setPresent(analyses.map((a) => a.id));
  };

  /** "New from template": the host's seeded copy already has resolved paths,
   *  so copying it is the one way to start from a sample without placeholders. */
  async function copyAnalysis(id: string): Promise<void> {
    const copy = freshCopyId(id, analyses.map((a) => a.id));
    try {
      await api.putAnalysis(copy, await api.getAnalysis(id));
      await refreshAnalyses();
      await openAnalysis(copy);
    } catch (e) {
      fail(`Could not copy '${id}': ${e instanceof Error ? e.message : e}`);
    }
  }

  async function openAnalysis(id: string): Promise<void> {
    resultSelection = [];
    lastPrimary = null;
    shownChoice = initialShownChoice();
    paneArea.showNode(null);
    connection?.dispose();
    connection = null;
    opening?.abort();
    const thisOpen = new AbortController();
    opening = thisOpen;
    // Panes read results by (currentId, node id). The new document enters the
    // store inside connectAnalysis, and the first pane read follows at once, so
    // the id must name this analysis before then, or the read asks the previous
    // analysis for a node it does not have.
    currentId = id;
    try {
      connection = await connectAnalysis(store, api, id, {
        autosaveMs: AUTOSAVE_MS,
        onSaveError: (e) => fail(`Autosave failed: ${e instanceof Error ? e.message : e}`),
        onStreamError: () => host.reportFailure("evaluation stream lost"),
        signal: thisOpen.signal,
      });
      reportSession();
      canvasEditor.refreshSuggestions();
      sidebar.flowOpened(store.getState().document.structure.nodes.length > 0);
      topbar.setAnalyses(analyses, id);
      if (options.syncUrl) {
        const loc = root.ownerDocument.defaultView?.location;
        if (loc) root.ownerDocument.defaultView!.history.replaceState(null, "", loc.pathname + searchWithAnalysis(loc.search, id) + loc.hash);
      }
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
      } else {
        // TKT-81: the plain editor never auto-fit on open, so a graph wider
        // than the canvas left its answer node off-screen to the right.
        canvasEditor.fit();
      }
    } catch (e) {
      if (thisOpen.signal.aborted) return; // a newer open replaced this one
      currentId = null;
      if (connectedNow()) fail(`Could not open flow '${id}': ${e instanceof Error ? e.message : e}`);
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
    // Add, place, and select are one undo step (the batch action).
    dispatch({ type: "batch", actions: addNodeActions(state, desc, freeSpot(state, catalog, desc)) });
    root.classList.remove("bof-app-catalog-open");
    shell.graphToolbar.querySelector("button")?.setAttribute("aria-expanded", "false");
  }

  // The last pointer position over the canvas, where Space opens the palette.
  let canvasPointer: { x: number; y: number } | null = null;
  const onCanvasPointerMove = (e: PointerEvent) => { canvasPointer = { x: e.clientX, y: e.clientY }; };
  const onCanvasPointerLeave = () => { canvasPointer = null; };
  shell.canvas.addEventListener("pointermove", onCanvasPointerMove);
  shell.canvas.addEventListener("pointerleave", onCanvasPointerLeave);

  // Undo/redo and palette shortcuts (skipped while typing in a field).
  const onKeyDown = (e: KeyboardEvent) => {
    const target = e.target as HTMLElement | null;
    if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
    if (paletteKeyOpens(e, shell.canvas)) {
      if (!currentId || palette.isOpen()) return;
      e.preventDefault(); // Space would otherwise scroll the page
      palette.open(paletteClientPoint(shell.canvas, canvasPointer), undefined);
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "z") {
      dispatch({ type: e.shiftKey ? "redo" : "undo" });
      e.preventDefault();
    } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "y") {
      dispatch({ type: "redo" });
      e.preventDefault();
    }
  };
  root.ownerDocument.addEventListener("keydown", onKeyDown);

  // A tab that becomes visible again resends the session unchanged, in case
  // it is the most recently reported editor and another tab took over.
  const onVisibilityChange = () => {
    if (root.ownerDocument.visibilityState === "visible") sessionReporter.flush();
  };
  root.ownerDocument.addEventListener("visibilitychange", onVisibilityChange);

  // ── boot and reconnect ─────────────────────────────────────────────────────
  // Connectivity failures are shown by the host banner, so error toasts are
  // reserved for failures that happened while the host was reachable.
  const connectedNow = () => host.get().status === "connected";
  let booted = false;

  const boot = async () => {
    try {
      const [, cat] = await Promise.all([refreshAnalyses(), api.getNodeCatalog()]);
      catalog.clear();
      for (const n of cat.nodes) catalog.set(n.kind, n);
      if (options.graphDemo && catalog.has("view3d.section") && catalog.get("view3d.section")?.params.find(p => p.name === "fraction")?.control?.kind !== "slider")
        fail("The 3D backend is out of date. Rebuild and restart BimOpenFlow.Host, then reload this page to enable the node controls.");
      sidebar.setCatalog(cat.nodes);
      canvasEditor.refresh();
      booted = true;
      // Always land in an open analysis so no click can fail for lack of one;
      // if the stored analysis fails to open, fall back to a fresh one.
      const { open, missing } = chooseInitialAnalysis(options.initialAnalysis, analyses.map(a => a.id));
      if (missing && options.graphDemo) {
        fail(`Snowdon graph is missing. Start the BIM-profile host with a Snowdon model and an empty store, or import snowdon-toolkit.json (see docs/bim-flow-3d.md).`);
        return;
      }
      if (missing) fail(`Analysis "${missing}" not found on this host; opened the default instead.`);
      if (open) {
        await openAnalysis(open);
        // A newcomer sees what the host offers before the first graph
        // (workflow 1: "a start page lists the supported demos").
        if (!options.graphDemo && !options.initialAnalysis) startPage.show();
      }
      if (currentId === null) await newAnalysis();
    } catch (e) {
      if (connectedNow()) fail(`Could not load the host: ${e instanceof Error ? e.message : e}`);
    }
  };

  // After a host restart the list and the open analysis's server state may be
  // stale, so a reconnect re-reads both; a boot that never completed is retried.
  // Reopening rebuilds the editor, so the selected node is carried across.
  const resync = async () => {
    if (!booted) return boot();
    try {
      await refreshAnalyses();
      if (currentId) await reopenKeepingSelection(store, () => openAnalysis(currentId!));
    } catch (e) {
      if (connectedNow()) fail(`Could not refresh after reconnecting: ${e instanceof Error ? e.message : e}`);
    }
  };

  // TKT-84: an Ask panel that edits the open flow, shown only when the host
  // answers /api/ask (mountAskPanel probes and mounts nothing otherwise).
  // TKT-112: it docks at the top of the right column, above the pane tabs.
  let askPanel: AskPanel | undefined;
  let askPanelDisposed = false;
  void mountAskPanel(shell.askHost, {
    host,
    getAnalysisId: () => currentId ?? undefined,
    onBuilt: async (id) => { await refreshAnalyses(); await openAnalysis(id); },
    onError: fail,
  }).then((panel) => { if (askPanelDisposed) panel.dispose(); else askPanel = panel; });

  let syncing: Promise<void> | null = null;
  const syncOnce = () => {
    if (syncing) return;
    syncing = resync().finally(() => { syncing = null; });
  };

  let lastStatus = host.get().status;
  topbar.setConnection(lastStatus);
  const unsubscribeHost = host.subscribe((state) => {
    topbar.setConnection(state.status);
    if (state.status === "connected" && lastStatus !== "connected") syncOnce();
    lastStatus = state.status;
  });
  syncOnce();

  return {
    openAnalysis,
    refreshAnalyses,
    session: () => currentSession,
    dispose() {
      askPanelDisposed = true;
      askPanel?.dispose();
      unsubscribe();
      unsubscribeHost();
      if (!options.host) host.dispose();
      opening?.abort();
      connection?.dispose();
      canvasEditor.dispose();
      palette.dispose();
      stepList.dispose();
      problems.dispose();
      startPage.dispose();
      paneArea.dispose();
      shell.dispose();
      sessionReporter.dispose();
      root.ownerDocument.removeEventListener("keydown", onKeyDown);
      shell.canvas.removeEventListener("pointermove", onCanvasPointerMove);
      shell.canvas.removeEventListener("pointerleave", onCanvasPointerLeave);
      root.ownerDocument.removeEventListener("visibilitychange", onVisibilityChange);
    },
  };
}
