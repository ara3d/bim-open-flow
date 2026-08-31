// Pure 3D-pane logic: instance-table parsing, per-group color/isolation
// plans, and pick-to-entity mapping. No viewer or WebGL dependency, so it is
// fully testable headless.
import type { ColumnSchema, TableSlice } from "@bimopenflow/contracts";
import { columnIndex } from "./columns";

/** The subset of viewer-core's InstancedGroup the color logic needs. */
export interface ColorableGroup {
  readonly instanceCount: number;
  /** Live RGBA view, 4 floats per instance. */
  readonly colors: Float32Array;
  setColors(start: number, colors: Float32Array): void;
}

/** One group's instances mapped back to model entity ids (loaders' groupEntities). */
export interface GroupEntityMap {
  readonly group: ColorableGroup;
  readonly entities: readonly number[];
}

export type Rgba = readonly [number, number, number, number];

/**
 * What an instance table asks of the scene: the visible entity keys
 * (isolation) and, when r/g/b/a columns are present, per-entity colors.
 */
export interface InstancePlan {
  readonly keys: ReadonlySet<number>;
  readonly colors: ReadonlyMap<number, Rgba> | null;
}

const COLOR_COLUMNS = ["r", "g", "b", "a"] as const;

/** Instance tables are keyed by "entityId" when present, else "instanceIndex". */
export const instanceKeyColumn = (columns: readonly ColumnSchema[]): number => {
  const e = columnIndex(columns, "entityId");
  if (e >= 0) return e;
  const i = columnIndex(columns, "instanceIndex");
  if (i < 0)
    throw new Error(
      'bof-panes: instance table needs an "entityId" or "instanceIndex" column',
    );
  return i;
};

/**
 * Parses an instance table (per src/BimOpenFlow.Nodes.Geometry/README.md)
 * into a plan: rows present define the visible set; r/g/b/a columns (0..1
 * floats), when all four are present, define per-entity colors.
 */
export const planFromSlice = (slice: TableSlice): InstancePlan => {
  const keyIdx = instanceKeyColumn(slice.columns);
  const colorIdx = COLOR_COLUMNS.map((name) => columnIndex(slice.columns, name));
  const hasColors = colorIdx.every((i) => i >= 0);
  const keys = new Set<number>();
  const colors = hasColors ? new Map<number, Rgba>() : null;
  for (const row of slice.rows) {
    const key = Number(row[keyIdx]);
    if (!Number.isFinite(key)) continue;
    keys.add(key);
    colors?.set(
      key,
      colorIdx.map((i) => Number(row[i])) as unknown as Rgba,
    );
  }
  return { keys, colors };
};

/**
 * New RGBA buffer for one group given its entity mapping, its colors as
 * loaded (`baseColors`), and a plan: entities in the plan get the plan color
 * (or their base color when the plan has none); entities not in the plan are
 * hidden via alpha 0. Returns null for an empty group.
 */
export const groupColorPlan = (
  entities: readonly number[],
  baseColors: Float32Array,
  plan: InstancePlan,
): Float32Array | null => {
  if (entities.length === 0) return null;
  const out = new Float32Array(entities.length * 4);
  entities.forEach((entity, i) => {
    const o = i * 4;
    const color = plan.keys.has(entity)
      ? plan.colors?.get(entity) ??
        ([baseColors[o], baseColors[o + 1], baseColors[o + 2], baseColors[o + 3]] as Rgba)
      : ([baseColors[o], baseColors[o + 1], baseColors[o + 2], 0] as Rgba);
    out.set(color, o);
  });
  return out;
};

/** Entity id for a picked (group, instanceIndex), or undefined when unmapped. */
export const entityForPick = (
  maps: readonly GroupEntityMap[],
  group: ColorableGroup,
  instanceIndex: number,
): number | undefined =>
  maps.find((m) => m.group === group)?.entities[instanceIndex];
