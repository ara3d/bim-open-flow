// Pure parsing of a boxes table (src/BimOpenFlow.Nodes.Geometry/README.md,
// "Boxes table columns") into instanced-unit-cube transforms and colors.
// No viewer or WebGL dependency, so it is fully testable headless.
import type { MeshBuffers } from "@ara3d/viewer-core";
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

const cubeFace = (
  // Face normal axis and direction; u/v span the face.
  normal: readonly [number, number, number],
  u: readonly [number, number, number],
  v: readonly [number, number, number],
): { positions: number[]; normals: number[] } => {
  const positions: number[] = [];
  const normals: number[] = [];
  for (const [su, sv] of [[-1, -1], [1, -1], [1, 1], [-1, 1]] as const)
    for (let c = 0; c < 3; c++) {
      positions.push(0.5 * normal[c] + 0.5 * su * u[c] + 0.5 * sv * v[c]);
      normals.push(normal[c]);
    }
  return { positions, normals };
};

const buildUnitCube = (): MeshBuffers => {
  const faces = [
    cubeFace([1, 0, 0], [0, 1, 0], [0, 0, 1]),
    cubeFace([-1, 0, 0], [0, 0, 1], [0, 1, 0]),
    cubeFace([0, 1, 0], [0, 0, 1], [1, 0, 0]),
    cubeFace([0, -1, 0], [1, 0, 0], [0, 0, 1]),
    cubeFace([0, 0, 1], [1, 0, 0], [0, 1, 0]),
    cubeFace([0, 0, -1], [0, 1, 0], [1, 0, 0]),
  ];
  const positions = new Float32Array(faces.flatMap((f) => f.positions));
  const normals = new Float32Array(faces.flatMap((f) => f.normals));
  const indices = new Uint32Array(
    faces.flatMap((_, f) => {
      const b = f * 4;
      return [b, b + 1, b + 2, b, b + 2, b + 3];
    }),
  );
  return { positions, normals, indices };
};

/** Axis-aligned cube: edge 1, centered at the origin, flat-shaded (24 verts, 12 tris). */
export const UNIT_CUBE: MeshBuffers = buildUnitCube();
