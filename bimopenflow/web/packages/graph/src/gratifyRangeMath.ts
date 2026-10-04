// Copied from deps/gratify/examples/shared/ (planned debt in docs/plans/graph-editor-package.md:
// the package must not import gratify's example code; TKT-23 rebuilds the slider and range as
// this package's own parts and deletes this copy).
// Pure thumb math for a dual-thumb range control (min..max inside [lo..hi]).
// No imports, no framework types — kernel-testable, and small enough to be
// replicated verbatim by downstream apps that consume gratify as a dist
// package (examples/ are not part of the published surface).
//
// Conventions:
//   value — a number in the domain [lo, hi]
//   frac  — a 0..1 position along the track
//   px    — a screen x inside a track that starts at `x` and is `w` wide

export interface RangeDomain {
  lo: number;
  hi: number;
  /** Optional quantization step; values snap to lo + k*step. */
  step?: number;
}

const clamp = (v: number, a: number, b: number) => Math.min(b, Math.max(a, v));

/** Snap a value to the domain's step grid (if any), clamped to [lo, hi]. */
export function quantize(value: number, d: RangeDomain): number {
  const v = clamp(value, d.lo, d.hi);
  if (!d.step || d.step <= 0) return v;
  return clamp(d.lo + Math.round((v - d.lo) / d.step) * d.step, d.lo, d.hi);
}

/** Domain value → 0..1 track fraction (0 when the domain is degenerate). */
export function fracOfValue(value: number, d: RangeDomain): number {
  const span = d.hi - d.lo;
  return span > 0 ? clamp((value - d.lo) / span, 0, 1) : 0;
}

/** 0..1 track fraction → quantized domain value. */
export function valueOfFrac(f: number, d: RangeDomain): number {
  return quantize(d.lo + clamp(f, 0, 1) * (d.hi - d.lo), d);
}

/** Domain value → screen x inside a track at `x` of width `w`. */
export const pxOfValue = (value: number, d: RangeDomain, x: number, w: number): number =>
  x + fracOfValue(value, d) * w;

/** Screen x → quantized domain value for a track at `x` of width `w`. */
export const valueOfPx = (px: number, d: RangeDomain, x: number, w: number): number =>
  valueOfFrac(w > 0 ? (px - x) / w : 0, d);

// ---- zone picking ------------------------------------------------------------

export type RangeZone = "min" | "max" | "window";

/**
 * Which part of the control a press at fraction `f` grabs, given thumb
 * fractions `fMin`/`fMax` and a grab radius `grab` (also in fraction units,
 * typically thumbRadiusPx / trackWidthPx).
 *
 *   left of min thumb  → "min"        right of max thumb → "max"
 *   between, near an edge (≤ grab, and nearer than the other) → that thumb
 *   between, far from both edges → "window" (drag shifts both)
 */
export function zoneAt(f: number, fMin: number, fMax: number, grab: number): RangeZone {
  if (f <= fMin) return "min";
  if (f >= fMax) return "max";
  const dMin = f - fMin, dMax = fMax - f;
  if (dMin <= grab && dMin <= dMax) return "min";
  if (dMax <= grab) return "max";
  return "window";
}

// ---- drag updates ------------------------------------------------------------

/** New min from a drag: quantized, clamped to the domain, can't cross max. */
export function dragMin(value: number, max: number, d: RangeDomain): { min: number; max: number } {
  return { min: Math.min(quantize(value, d), max), max };
}

/** New max from a drag: quantized, clamped to the domain, can't cross min. */
export function dragMax(min: number, value: number, d: RangeDomain): { min: number; max: number } {
  return { min, max: Math.max(quantize(value, d), min) };
}

/**
 * Shift the whole window by `delta` (domain units), preserving its width
 * exactly and staying inside [lo, hi]. Min snaps to the step grid best-effort
 * (the exact-width guarantee wins over the grid at the hi edge).
 */
export function shiftWindow(
  min: number, max: number, delta: number, d: RangeDomain,
): { min: number; max: number } {
  const w = max - min;
  const room = d.hi - d.lo;
  const width = clamp(w, 0, room);
  const nmin = Math.min(quantize(clamp(min + delta, d.lo, d.hi - width), d), d.hi - width);
  return { min: nmin, max: nmin + width };
}
