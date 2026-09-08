import type { TableSlice } from "@bimopenflow/contracts";
import type { ModelFormat, Pane } from "./pane";
import { definePane } from "./base";
import {
  groupColorPlan,
  groupTransformPlan,
  planFromSlice,
  type GroupEntityMap,
} from "./instanceTable";
import { parseBoxTable } from "./boxTable";
import { defaultView3DDeps, type View3DDeps } from "./viewerDeps";
import { parseViewRecipe } from "./viewRecipe";
import type { LegendEntry } from "./toolkitRecipe";

export interface ViewPane3DOptions {
  /** The graph owns presentation; do not offer temporary overrides of it. */
  followGraph?: boolean;
  /** Viewer wiring override, mainly for headless tests. */
  deps?: View3DDeps;
}

/** ".bos" (any case, query/hash ignored) loads as BOS; everything else as GLB. */
export const inferFormat = (url: string): ModelFormat =>
  /\.bfast$/i.test(url.split(/[?#]/, 1)[0]) ? "bfast" :
  /\.bos$/i.test(url.split(/[?#]/, 1)[0]) ? "bos" : "glb";

/**
 * 3D view pane: a Viewer with orbit and pick controls on a canvas.
 *
 * Inputs: "model" loads a model via ctx.resolveAsset (format from the URL
 * unless given); "instances" applies an instance table — rows define the
 * visible (isolated) set, r/g/b/a columns recolor, an `a` column alone fades
 * (0 hides), offsetX/Y/Z columns translate instances on top of the loaded
 * transforms, and absent instances get alpha 0. "boxes" renders a boxes
 * table as instanced unit cubes, replacing any previous boxes group. An
 * instances input arriving before the model finishes loading is applied
 * afterwards. Emits "selection" (ids = [entityId]) on pick where the
 * loader provided a group→entity mapping, and "action" modelLoaded/loadError.
 */
export const createViewPane3D = (options?: ViewPane3DOptions): Pane =>
  definePane((root, ctx, emit) => {
    const deps = options?.deps ?? defaultView3DDeps;
    const canvas = root.ownerDocument.createElement("canvas");
    canvas.className = "bof-panes-canvas";
    canvas.tabIndex = 0;
    canvas.setAttribute("aria-label", "3D model. Drag to orbit, scroll to zoom.");
    root.classList.add("bof-panes-view3d");
    const toolbar = root.ownerDocument.createElement("div");
    toolbar.className = "bof-panes-toolbar";
    const status = root.ownerDocument.createElement("div");
    status.className = "bof-panes-viewstatus";
    status.setAttribute("role", "status");
    status.textContent = "Choose a model or view recipe.";
    root.append(toolbar, status);
    root.appendChild(canvas);
    const legend = root.ownerDocument.createElement("div");
    legend.className = "bof-panes-legend";
    legend.setAttribute("aria-label", "Source category legend");
    root.append(legend);
    let displayedLegend: readonly LegendEntry[] | undefined;
    const updateLegend = () => {
      const entries = rig.legend?.() ?? [];
      if (entries === displayedLegend) return;
      displayedLegend = entries;
      legend.replaceChildren();
      for (const entry of entries) {
        const item = root.ownerDocument.createElement("span");
        const swatch = root.ownerDocument.createElement("i");
        swatch.style.background = `rgb(${entry.color.map(c => Math.round(c * 255)).join(",")})`;
        item.append(swatch, `${entry.name} (${entry.count.toLocaleString()} objects)`);
        legend.append(item);
      }
      legend.hidden = !legend.children.length;
    };

    const reportError = (error: unknown) => {
      status.textContent = String(error);
      status.setAttribute("role", "alert");
      emit({ kind: "action", action: "loadError", payload: { message: String(error) } });
    };
    let rig: ReturnType<View3DDeps["createRig"]>;
    try { rig = deps.createRig(canvas, (entityId) => {
      if (entityId !== null) status.textContent = `Selected entity ${entityId}`;
      if (entityId !== null)
        emit({
          kind: "selection",
          event: { source: "view3d", ids: [String(entityId)] },
        });
    }); } catch (error) {
      reportError(error);
      return { update() {}, destroy() {} };
    }

    let maps: readonly GroupEntityMap[] = [];
    let baseColors: Float32Array[] = [];
    let baseTransforms: (Float32Array | null)[] = [];
    let offsetsApplied = false;
    let pending: TableSlice | null = null;
    let loadToken = 0;
    let loading = false;
    let pendingView: TableSlice | null = null;
    let pendingBoxes: TableSlice | null = null;
    let lastModel: { url: string; format: ModelFormat } | null = null;
    let disposed = false;
    let recipeFrame: number | null = null;
    const scheduleRecipe = () => {
      const win = root.ownerDocument.defaultView;
      if (!options?.followGraph || !win?.requestAnimationFrame) {
        if (pendingView) rig.applyRecipe?.(pendingView);
        updateLegend();
        return;
      }
      if (recipeFrame !== null) return;
      recipeFrame = win.requestAnimationFrame(() => {
        recipeFrame = null;
        if (disposed || loading || !pendingView) return;
        try { rig.applyRecipe?.(pendingView); updateLegend(); } catch (error) { reportError(error); }
      });
    };
    const button = (label: string, action: () => void | Promise<void>) => {
      const control = root.ownerDocument.createElement("button");
      control.type = "button";
      control.textContent = label;
      control.addEventListener("click", () => {
        Promise.resolve().then(action).catch(error => { if (!disposed) reportError(error); });
      });
      toolbar.append(control);
    };
    if (rig.fit) button("Fit", () => rig.fit?.());
    if (rig.reset && !options?.followGraph) button("Reset view", () => {
      rig.reset?.();
      updateLegend();
      status.textContent = "Original view restored. Reapply to restore the graph presentation.";
    });
    if (rig.applyRecipe && !options?.followGraph) button("Reapply graph", () => {
      if (pending) applyInstances(pending);
      if (pendingBoxes) applyBoxes(pendingBoxes);
      if (pendingView) rig.applyRecipe?.(pendingView);
      updateLegend();
    });
    if (rig.capture) button("Save PNG", async () => {
      const blob = await rig.capture!();
      if (disposed) return;
      const url = URL.createObjectURL(blob);
      const link = root.ownerDocument.createElement("a");
      link.href = url;
      link.download = "bim-flow-view.png";
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 0);
    });
    button("Retry model", () => {
      if (lastModel) load(lastModel.url, lastModel.format);
    });

    const applyInstances = (slice: TableSlice): void => {
      const plan = planFromSlice(slice);
      const applyOffsets = plan.offsets !== null || offsetsApplied;
      maps.forEach((m, i) => {
        const colors = groupColorPlan(m.entities, baseColors[i], plan);
        if (colors) m.group.setColors(0, colors);
        const base = baseTransforms[i];
        if (!applyOffsets || !base || !m.group.setTransform) return;
        const transforms = groupTransformPlan(m.entities, base, plan) ?? base;
        for (let j = 0; j < m.entities.length; j++)
          m.group.setTransform(j, transforms.subarray(j * 16, (j + 1) * 16));
      });
      offsetsApplied = plan.offsets !== null;
      rig.requestRender();
    };

    const applyBoxes = (slice: TableSlice) => {
      const boxes = parseBoxTable(slice);
      if (boxes.count === 0) rig.clearBoxes();
      else rig.setBoxes(boxes.transforms, boxes.colors);
      rig.requestRender();
    };
    const load = (url: string, format: ModelFormat) => {
      lastModel = { url, format };
      const token = ++loadToken;
      maps = [];
      loading = true;
      status.setAttribute("role", "status");
      status.textContent = "Loading model…";
      rig.load(ctx.resolveAsset(url), format).then(loaded => {
        if (disposed || token !== loadToken) return;
        maps = loaded;
        loading = false;
        baseColors = loaded.map(m => m.group.colors.slice());
        baseTransforms = loaded.map(m => m.group.transforms?.slice() ?? null);
        offsetsApplied = false;
        if (pending) applyInstances(pending);
        if (pendingBoxes) applyBoxes(pendingBoxes);
        if (pendingView) rig.applyRecipe?.(pendingView);
        updateLegend();
        status.textContent = `${loaded.reduce((n, m) => n + m.entities.length, 0).toLocaleString()} instances · orbit / pan / zoom`;
        emit({ kind: "action", action: "modelLoaded", payload: { url } });
      }).catch(error => {
        if (disposed || token !== loadToken) return;
        loading = false;
        status.textContent = String(error);
        status.setAttribute("role", "alert");
        emit({ kind: "action", action: "loadError", payload: { url, message: String(error) } });
      });
    };

    return {
      update(input) {
        if (input.kind === "model") {
          if (lastModel) {
            pending = null;
            pendingBoxes = null;
            pendingView = null;
          }
          load(input.url, input.format ?? inferFormat(input.url));
        } else if (input.kind === "instances") {
          pending = input.data;
          if (!loading && maps.length > 0) applyInstances(input.data);
        } else if (input.kind === "boxes") {
          pendingBoxes = input.data;
          if (!loading) applyBoxes(input.data);
        } else if (input.kind === "view") {
          try {
            parseViewRecipe(input.data);
            pendingView = input.data;
            if (!loading && maps.length > 0) scheduleRecipe();
          } catch (error) { reportError(error); }
        }
      },
      destroy: () => {
        disposed = true; loadToken++;
        if (recipeFrame !== null) root.ownerDocument.defaultView?.cancelAnimationFrame(recipeFrame);
        rig.dispose();
      },
    };
  });
