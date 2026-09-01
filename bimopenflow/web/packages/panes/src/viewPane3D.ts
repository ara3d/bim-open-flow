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
    let baseTransforms: (Float32Array | null)[] = [];
    let offsetsApplied = false;
    let pending: TableSlice | null = null;
    let loadToken = 0;

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
              baseTransforms = loaded.map((m) => m.group.transforms?.slice() ?? null);
              offsetsApplied = false;
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
        } else if (input.kind === "boxes") {
          const boxes = parseBoxTable(input.data);
          if (boxes.count === 0) rig.clearBoxes();
          else rig.setBoxes(boxes.transforms, boxes.colors);
          rig.requestRender();
        }
      },
      destroy: () => rig.dispose(),
    };
  });
