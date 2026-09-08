import { objectKey, styleRule, translation, type Bounds, type ModelData, type Result, type StyleRule, type Vec3 } from "@bim-open-toolkit/model";
import { clippingFeature, clippingSlice, environmentFeature, environmentSlice, layoutsFeature, layoutsHook } from "@bim-open-toolkit/features";
import { defaultEnvironment, dirtySets, publishDirty, noClipping, type InstanceTable } from "@bim-open-toolkit/render";
import { setProjectionKind } from "@bim-open-toolkit/interact";
import type { Viewer } from "@bim-open-toolkit/viewer";
import { sectionElevation, type ViewStep } from "./viewRecipe";

export const recipeFeatures = [clippingFeature, environmentFeature, layoutsFeature];
export type LegendEntry = { name: string; color: Vec3; count: number };

export function requireResult<T>(result: Result<T>): T {
  if (!result.ok) throw new Error(result.diagnostics.map(d => d.message).join("; "));
  return result.value;
}

const palette: readonly Vec3[] = [[.18,.55,.83],[.94,.55,.22],[.24,.7,.55],[.65,.4,.78],[.85,.36,.45],[.63,.63,.24]];
export function categoryRules(model: ModelData, opacity: number): readonly StyleRule[] {
  const groups = new Map<string, string[]>();
  for (const record of model.objects) {
    const category = record.category ?? "Unknown category";
    const keys = groups.get(category) ?? [];
    keys.push(objectKey(record.ref));
    groups.set(category, keys);
  }
  return [...groups].sort(([a], [b]) => a.localeCompare(b)).map(([name, keys], i) =>
    styleRule("category-" + i, name, keys, { color: name === "Unknown category" ? [.5,.5,.5] : palette[i % palette.length], opacity }));
}

/** BOS records have identity transforms. Layouts need the placement translation from their geometry. */
export function layoutModel(model: ModelData, table: InstanceTable): ModelData {
  return { ...model, objects: model.objects.map((record, ordinal) => {
    const at = table.objectStart[ordinal];
    if (at === undefined || at === table.objectStart[ordinal + 1]) return record;
    const row = table.objectRows[at];
    const group = table.groupOfRow[row];
    const values = table.transforms[group];
    const offset = (row - table.groupStart[group]) * 16 + 12;
    return { ...record, transform: translation([values[offset], values[offset + 1], values[offset + 2]]) };
  }) };
}

export function mountRecipe(viewer: Viewer, model: ModelData, table: InstanceTable, restoreSource: () => void = () => {}) {
  let legend: readonly LegendEntry[] = [];
  const dirty = dirtySets(table);
  const hook = layoutsHook({ model: layoutModel(model, table), table, dirty, moved: () => {
    publishDirty(table, dirty);
    dirty.transforms.reset();
    viewer.views.requestRender();
  } })(viewer.session);
  const originalBounds: Bounds = viewer.bounds();
  const initialCamera = viewer.views.all()[0]?.camera();
  const run = (name: string, input: unknown = {}) => requireResult(viewer.run(name, input));
  const reset = () => {
    legend = [];
    run("layouts.reset");
    run("appearance.clear");
    restoreSource();
    run("clipping.clear");
    viewer.setClipping(noClipping);
    viewer.setEnvironment({ ...defaultEnvironment, up: model.coordinates.up });
    run("view.mode", { mode: "orbit" });
    if (initialCamera) run("view.look", { camera: initialCamera });
  };
  return {
    legend: () => legend,
    reset,
    dispose: () => hook.dispose(),
    apply(steps: readonly ViewStep[]) {
      reset();
      for (const step of steps) {
        switch (step.operation) {
          case "scene": break;
          case "section":
            run("clipping.sectionAt", { axis: step.input.axis, elevation: sectionElevation(originalBounds, step.input.axis, step.input.fraction) });
            viewer.setClipping(viewer.session.read(clippingSlice).region);
            break;
          case "sectionBox": {
            const half = step.input.fraction / 2;
            const min: Vec3 = [sectionElevation(originalBounds, "x", .5-half), sectionElevation(originalBounds, "y", .5-half), sectionElevation(originalBounds, "z", .5-half)];
            const max: Vec3 = [sectionElevation(originalBounds, "x", .5+half), sectionElevation(originalBounds, "y", .5+half), sectionElevation(originalBounds, "z", .5+half)];
            run("clipping.setBox", { min, max });
            viewer.setClipping(viewer.session.read(clippingSlice).region);
            break;
          }
          case "explode":
            run("layouts.explode", step.input);
            run("view.fit");
            break;
          case "projection": {
            const view = viewer.views.all()[0];
            if (!view) throw new Error("No 3D view is available.");
            run("view.mode", { mode: step.input.mode === "plan" ? "overhead" : "orbit" });
            run("view.look", { camera: setProjectionKind(view.camera(), step.input.mode === "perspective" ? "perspective" : "orthographic") });
            run("view.fit");
            break;
          }
          case "environment":
            run("environment.set", { background: step.input.theme === "dark" ? [.06,.08,.12] : [.94,.95,.97], grid: { enabled: step.input.grid }, axes: true, up: model.coordinates.up });
            viewer.setEnvironment(viewer.session.read(environmentSlice).settings);
            break;
          case "categoryStyle": {
            const rules = categoryRules(model, step.input.opacity);
            requireResult(viewer.apply(...rules));
            legend = rules.map(rule => ({ name: rule.name, color: rule.change.color ?? [.5,.5,.5], count: rule.targets.length }));
            break;
          }
        }
      }
    },
  };
}
