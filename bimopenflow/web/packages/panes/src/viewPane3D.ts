import type { TableSlice } from "@bimopenflow/contracts";
import type { ModelFormat, Pane } from "./pane";
import { definePane } from "./base";
import { groupColorPlan, planFromSlice, type GroupEntityMap } from "./instanceTable";
import { defaultView3DDeps, type View3DDeps } from "./viewerDeps";

export interface ViewPane3DOptions {
  /** Viewer wiring override, mainly for headless tests. */
  deps?: View3DDeps;
}

/** ".bos" (any case, query/hash ignored) loads as BOS; everything else as GLB. */
export const inferFormat = (url: string): ModelFormat =>
  /\.bos$/i.test(url.split(/[?#]/, 1)[0]) ? "bos" : "glb";

/**
 * 3D view pane: a Viewer with orbit and pick controls on a canvas.
 *
 * Inputs: "model" loads a model via ctx.resolveAsset (format from the URL
 * unless given); "instances" applies an instance table — rows define the
 * visible (isolated) set, r/g/b/a columns recolor, absent instances get
 * alpha 0. An instances input arriving before the model finishes loading is
 * applied afterwards. Emits "selection" (ids = [entityId]) on pick where the
 * loader provided a group→entity mapping, and "action" modelLoaded/loadError.
 */
export const createViewPane3D = (options?: ViewPane3DOptions): Pane =>
  definePane((root, ctx, emit) => {
    const deps = options?.deps ?? defaultView3DDeps;
    const canvas = root.ownerDocument.createElement("canvas");
    canvas.className = "bof-panes-canvas";
    root.appendChild(canvas);

    const rig = deps.createRig(canvas, (entityId) => {
      if (entityId !== null)
        emit({
          kind: "selection",
          event: { source: "view3d", ids: [String(entityId)] },
        });
    });

    let maps: readonly GroupEntityMap[] = [];
    let baseColors: Float32Array[] = [];
    let pending: TableSlice | null = null;
    let loadToken = 0;

    const applyInstances = (slice: TableSlice): void => {
      const plan = planFromSlice(slice);
      maps.forEach((m, i) => {
        const colors = groupColorPlan(m.entities, baseColors[i], plan);
        if (colors) m.group.setColors(0, colors);
      });
      rig.requestRender();
    };

    return {
      update(input) {
        if (input.kind === "model") {
          const token = ++loadToken;
          const url = ctx.resolveAsset(input.url);
          rig.load(url, input.format ?? inferFormat(input.url)).then(
            (loaded) => {
              if (token !== loadToken) return;
              maps = loaded;
              baseColors = loaded.map((m) => m.group.colors.slice());
              if (pending) {
                const slice = pending;
                pending = null;
                applyInstances(slice);
              }
              emit({
                kind: "action",
                action: "modelLoaded",
                payload: { url: input.url },
              });
            },
            (err) =>
              emit({
                kind: "action",
                action: "loadError",
                payload: { url: input.url, message: String(err) },
              }),
          );
        } else if (input.kind === "instances") {
          if (maps.length === 0) pending = input.data;
          else applyInstances(input.data);
        }
      },
      destroy: () => rig.dispose(),
    };
  });
