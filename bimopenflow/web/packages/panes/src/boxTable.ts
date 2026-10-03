// Pure parsing of a boxes table (src/BimOpenFlow.Nodes.Geometry/README.md,
// "Boxes table columns") into instanced-unit-cube transforms and colors.
// No viewer or WebGL dependency; the cube itself is pane-3d's UNIT_CUBE.
import type { TableSlice } from "@bimopenflow/contracts";
import { columnIndex } from "./columns";

const BOUNDS_COLUMNS = ["minX", "minY", "minZ", "maxX", "maxY", "maxZ"] as const;
const COLOR_COLUMNS = ["r", "g", "b", "a"] as const;
const DEFAULT_COLOR = [0.7, 0.7, 0.7, 1] as const;

/** What a boxes table asks of the scene: one unit-cube instance per row. */
export interface BoxPlan {
  /** 16 floats per box, column-major: scale = extent, translation = center. */
  readonly transforms: Float32Array;
  /** RGBA per box; r/g/b/a columns when all four are present, gray otherwise. */
  readonly colors: Float32Array;
  readonly count: number;
}

/**
 * A boxes table by shape: all six bounds columns, and none of the instance key
 * columns. Distinguishes derived boxes tables (which keep flowing through
 * generic table nodes like view3d.color, so the port name alone is not enough)
 * from instance tables, which also carry bounds.
 */
export const isBoxTable = (columns: TableSlice["columns"]): boolean =>
  BOUNDS_COLUMNS.every((name) => columnIndex(columns, name) >= 0) &&
  columnIndex(columns, "entityId") < 0 &&
  columnIndex(columns, "instanceIndex") < 0;

/**
 * Parses a boxes table: requires minX..maxZ (throws with the missing names
 * otherwise). Each row becomes a transform that scales the unit cube (edge 1,
 * centered at origin) to the box extent and moves it to the box center.
 */
export const parseBoxTable = (slice: TableSlice): BoxPlan => {
  const boundsIdx = BOUNDS_COLUMNS.map((name) => columnIndex(slice.columns, name));
  const missing = BOUNDS_COLUMNS.filter((_, i) => boundsIdx[i] < 0);
  if (missing.length > 0)
    throw new Error(
      `bof-panes: boxes table is missing required column(s): ${missing.join(", ")}`,
    );
  const colorIdx = COLOR_COLUMNS.map((name) => columnIndex(slice.columns, name));
  const hasColors = colorIdx.every((i) => i >= 0);
  const count = slice.rows.length;
  const transforms = new Float32Array(count * 16);
  const colors = new Float32Array(count * 4);
  slice.rows.forEach((row, i) => {
    const [minX, minY, minZ, maxX, maxY, maxZ] = boundsIdx.map((c) => Number(row[c]));
    const o = i * 16;
    transforms[o] = maxX - minX;
    transforms[o + 5] = maxY - minY;
    transforms[o + 10] = maxZ - minZ;
    transforms[o + 12] = (minX + maxX) / 2;
    transforms[o + 13] = (minY + maxY) / 2;
    transforms[o + 14] = (minZ + maxZ) / 2;
    transforms[o + 15] = 1;
    colors.set(
      hasColors ? colorIdx.map((c) => Number(row[c])) : DEFAULT_COLOR,
      i * 4,
    );
  });
  return { transforms, colors, count };
};
