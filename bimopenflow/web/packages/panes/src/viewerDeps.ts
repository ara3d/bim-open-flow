import { InstancedGroup, groupBounds } from "@ara3d/viewer-core";
import { fitBounds } from "@bim-open-toolkit/interact";
import { loadModel } from "@bim-open-toolkit/formats";
import { createViewer, defaultFeatures } from "@bim-open-toolkit/viewer";
import type { TableSlice } from "@bimopenflow/contracts";
import type { ModelFormat } from "./pane";
import type { GroupEntityMap } from "./instanceTable";
import { UNIT_CUBE } from "./boxTable";
import { mountRecipe, recipeFeatures, requireResult, type LegendEntry } from "./toolkitRecipe";
import { parseViewRecipe } from "./viewRecipe";
import { visibleSource, restoreSourceColors } from "./toolkitSource";

export interface ViewerRig {
  load(url: string, format: ModelFormat): Promise<readonly GroupEntityMap[]>;
  setBoxes(transforms: Float32Array, colors: Float32Array): void;
  clearBoxes(): void;
  requestRender(): void;
  applyRecipe?(table: TableSlice): void;
  fit?(): void;
  reset?(): void;
  capture?(): Promise<Blob>;
  legend?(): readonly LegendEntry[];
  dispose(): void;
}
export interface View3DDeps {
  createRig(canvas: HTMLCanvasElement, onPick: (entityId: number | null) => void): ViewerRig;
}

/** Public V2 composition owns model binding, resize, camera, clipping, capture and disposal. */
export const defaultView3DDeps: View3DDeps = {
  createRig(canvas, onPick) {
    const viewer = createViewer({ canvas, features: [...defaultFeatures(), ...recipeFeatures], selectOnClick: false });
    const startup = viewer.diagnostics().filter(d => d.severity === "error");
    if (startup.length) {
      viewer.dispose();
      throw new Error(startup.map(d => d.message).join("; "));
    }
    let recipe: ReturnType<typeof mountRecipe> | undefined;
    let boxes: InstancedGroup | undefined;
    let abort: AbortController | undefined;
    let generation = 0;
    let disposed = false;
    const listeners = new AbortController();
    let down: { x: number; y: number; id: number } | undefined;
    canvas.addEventListener("pointerdown", e => {
      down = e.button === 0 ? { x: e.clientX, y: e.clientY, id: e.pointerId } : undefined;
    }, { signal: listeners.signal });
    canvas.addEventListener("pointercancel", () => { down = undefined; }, { signal: listeners.signal });
    canvas.addEventListener("pointerup", e => {
      const start = down;
      down = undefined;
      if (!start || start.id !== e.pointerId || Math.hypot(e.clientX-start.x, e.clientY-start.y) > 4) return;
      const view = viewer.views.all()[0];
      const ndc = view?.ndc(e.clientX, e.clientY);
      const hit = ndc ? viewer.pick(ndc.x, ndc.y) : undefined;
      if (!hit) return;
      // BOS object IDs are source entity rows, not Revit sourceIds.
      const local = hit.key.split("|").at(-1);
      const id = local ? Number(decodeURIComponent(local).replace(/^bos:/, "")) : NaN;
      onPick(Number.isFinite(id) ? id : null);
    }, { signal: listeners.signal });
    const clearBoxes = () => {
      if (!boxes) return;
      viewer.views.removeGroups([boxes]);
      boxes = undefined;
    };
    const fit = () => {
      const view = viewer.views.all()[0];
      const bounds = boxes && viewer.models().length === 0 ? groupBounds(boxes) : undefined;
      if (view && bounds) {
        const camera = fitBounds(view.camera(), bounds, { aspect: view.aspect(), padding: 1.05 });
        if (camera) view.setCamera(camera);
      } else requireResult(viewer.run("view.fit"));
    };
    return {
      async load(url, format) {
        const token = ++generation;
        abort?.abort();
        abort = new AbortController();
        // Load without binding. A cancelled or superseded request cannot enter the scene.
        // BOS endpoints may serve verified prepared BFAST; detect their byte signature.
        const loaded = visibleSource(requireResult(await loadModel(url, { format: format === "bos" ? undefined : format, signal: abort.signal })));
        if (disposed || token !== generation) throw new Error("Model load superseded.");
        recipe?.dispose();
        recipe = undefined;
        clearBoxes();
        for (const model of viewer.models()) viewer.close(model.id);
        const opened = requireResult(viewer.show(loaded));
        const table = viewer.binding.tableOf(opened.ref.id);
        if (!table || table.rowCount === 0) throw new Error("The model contains no renderable geometry.");
        // Frame with the source coordinate convention, including z-up BOS.
        const view = viewer.views.all()[0];
        if (view) {
          const current = view.camera();
          view.setCamera({ ...current, coordinates: loaded.data.coordinates,
            camera: { ...current.camera, up: loaded.data.coordinates.up === "z" ? [0,0,1] : [0,1,0] } });
          requireResult(viewer.run("view.fit"));
        }
        const restore = () => restoreSourceColors(loaded, table);
        restore();
        recipe = mountRecipe(viewer, loaded.data, table, restore);
        return table.groups.map((group, groupIndex) => {
          const start = table.groupStart[groupIndex];
          const end = table.groupStart[groupIndex + 1];
          const entities: number[] = [];
          for (let row = start; row < end; row++) {
            const record = loaded.data.objects[table.objectOfRow[row]];
            entities.push(Number(record?.ref.objectId.replace(/^bos:/, "")));
          }
          return { entities, group: {
            instanceCount: group.instanceCount,
            colors: group.colors,
            transforms: group.transforms,
            setColors(first: number, colors: Float32Array) {
              for (let slot = 0; slot < colors.length / 4; slot++) {
                const row = start + first + slot;
                table.opacity[row] = colors[slot * 4 + 3];
                table.visible[row] = colors[slot * 4 + 3] > 0 ? 1 : 0;
              }
              group.setColors(first, colors);
            },
            setTransform: (index: number, matrix: Float32Array) => group.setTransform(index, matrix),
          } };
        });
      },
      applyRecipe(table) {
        const steps = parseViewRecipe(table);
        if (!recipe) throw new Error("Open a model before applying a view recipe.");
        recipe.apply(steps);
      },
      fit,
      reset: () => recipe?.reset(),
      legend: () => recipe?.legend() ?? [],
      async capture() {
        const image = requireResult(await viewer.capture());
        return new Blob([new Uint8Array(image.bytes)], { type: image.format });
      },
      setBoxes(transforms, colors) {
        clearBoxes();
        boxes = new InstancedGroup(UNIT_CUBE);
        boxes.append(transforms, colors);
        viewer.views.addGroups([boxes]);
        if (viewer.models().length === 0) {
          // Unbound analytical boxes have their own bounds, outside the model inventory.
          fit();
        }
      },
      clearBoxes,
      requestRender: () => viewer.views.requestRender(),
      dispose() {
        if (disposed) return;
        disposed = true;
        generation++;
        abort?.abort();
        listeners.abort();
        recipe?.dispose();
        clearBoxes();
        viewer.dispose();
      },
    };
  },
};
