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
  /** Live transform view, 16 floats per instance (optional; offsets need it). */
  readonly transforms?: Float32Array;
  setTransform?(index: number, matrix: Float32Array): void;
}

/** One group's instances mapped back to model entity ids (loaders' groupEntities). */
export interface GroupEntityMap {
  readonly group: ColorableGroup;
  readonly entities: readonly number[];
}

export type Rgba = readonly [number, number, number, number];

/**
 * What an instance table asks of the scene: the visible entity keys
 * (isolation); per-entity colors when all of r/g/b/a are present; per-entity
 * alphas whenever an `a` column exists (alone or alongside colors); and
 * per-entity translation offsets when all of offsetX/Y/Z are present.
 */
export interface InstancePlan {
  readonly keys: ReadonlySet<number>;
  readonly colors: ReadonlyMap<number, Rgba> | null;
  readonly alphas: ReadonlyMap<number, number> | null;
  readonly offsets: ReadonlyMap<number, readonly [number, number, number]> | null;
}

const COLOR_COLUMNS = ["r", "g", "b", "a"] as const;
const OFFSET_COLUMNS = ["offsetX", "offsetY", "offsetZ"] as const;

const finiteOr0 = (value: unknown): number => {
  const n = Number(value);
  return Number.isFinite(n) ? n : 0;
};

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
  const alphaIdx = columnIndex(slice.columns, "a");
  const offsetIdx = OFFSET_COLUMNS.map((name) => columnIndex(slice.columns, name));
  const hasOffsets = offsetIdx.every((i) => i >= 0);
  const keys = new Set<number>();
  const colors = hasColors ? new Map<number, Rgba>() : null;
  const alphas = alphaIdx >= 0 ? new Map<number, number>() : null;
  const offsets = hasOffsets
    ? new Map<number, readonly [number, number, number]>()
    : null;
  for (const row of slice.rows) {
    const key = Number(row[keyIdx]);
    if (!Number.isFinite(key)) continue;
    keys.add(key);
    colors?.set(
      key,
      colorIdx.map((i) => Number(row[i])) as unknown as Rgba,
    );
    alphas?.set(key, Number(row[alphaIdx]));
    offsets?.set(key, [
      finiteOr0(row[offsetIdx[0]]),
      finiteOr0(row[offsetIdx[1]]),
      finiteOr0(row[offsetIdx[2]]),
    ]);
  }
  return { keys, colors, alphas, offsets };
};

/**
 * New RGBA buffer for one group given its entity mapping, its colors as
 * loaded (`baseColors`), and a plan: entities in the plan get the plan color;
 * failing that, base rgb with the plan alpha (alpha-only tables); failing
 * that, their base color. Entities not in the plan are hidden via alpha 0.
 * Returns null for an empty group.
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
        ([
          baseColors[o],
          baseColors[o + 1],
          baseColors[o + 2],
          plan.alphas?.get(entity) ?? baseColors[o + 3],
        ] as Rgba)
      : ([baseColors[o], baseColors[o + 1], baseColors[o + 2], 0] as Rgba);
    out.set(color, o);
  });
  return out;
};

/**
 * New transform buffer (16 floats per instance, column-major) for one group:
 * each planned entity's offset is added to its base translation (elements
 * 12/13/14); entities without an offset keep their base transform. Returns
 * null when the plan carries no offsets.
 */
export const groupTransformPlan = (
  entities: readonly number[],
  baseTransforms: Float32Array,
  plan: InstancePlan,
): Float32Array | null => {
  if (!plan.offsets) return null;
  const out = baseTransforms.slice();
  entities.forEach((entity, i) => {
    const off = plan.offsets!.get(entity);
    if (!off) return;
    const o = i * 16;
    out[o + 12] += off[0];
    out[o + 13] += off[1];
    out[o + 14] += off[2];
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
