import { isInstanceVisible, noMesh } from "@bim-open-toolkit/model";
import type { LoadedModel } from "@bim-open-toolkit/formats";
import type { InstanceTable } from "@bim-open-toolkit/render";

/** Keep source-hidden placements out of the render binding and its fit bounds, retaining every object identity. */
export function visibleSource(model: LoadedModel): LoadedModel {
  const instances = model.geometry.instances;
  const meshIndex = instances.meshIndex.slice();
  for (let row = 0; row < instances.count; row++)
    if (!isInstanceVisible(instances, row)) meshIndex[row] = noMesh;
  return { ...model, geometry: { ...model.geometry, instances: { ...instances, meshIndex } } };
}

/** Restore per-placement materials; an object can have several differently colored placements. */
export function restoreSourceColors(model: LoadedModel, table: InstanceTable): void {
  const source = model.geometry.instances.color;
  table.groups.forEach((group, ordinal) => {
    const start = table.groupStart[ordinal];
    const end = table.groupStart[ordinal + 1];
    const colors = table.colors[ordinal];
    for (let row = start; row < end; row++) {
      const from = table.instanceOfRow[row] * 4;
      const to = (row - start) * 4;
      colors.set(source.subarray(from, from + 4), to);
      table.opacity[row] = source[from + 3];
      table.visible[row] = source[from + 3] > 0 ? 1 : 0;
    }
    group.setColors(0, colors);
  });
}
