/** Linear mapping from [d0, d1] to [r0, r1]. */
export const linearScale =
  (d0: number, d1: number, r0: number, r1: number) =>
  (v: number): number =>
    r0 + ((v - d0) / (d1 - d0)) * (r1 - r0);

const niceStep = (raw: number): number => {
  const pow = 10 ** Math.floor(Math.log10(raw));
  const r = raw / pow;
  return (r < 1.5 ? 1 : r < 3 ? 2 : r < 7 ? 5 : 10) * pow;
};

/** Round-valued tick positions covering [min, max], roughly `count` of them. */
export const niceTicks = (min: number, max: number, count = 5): number[] => {
  if (!(max > min)) return [min];
  const step = niceStep((max - min) / Math.max(1, count));
  const out: number[] = [];
  for (let v = Math.ceil(min / step) * step; v <= max + step * 1e-9; v += step)
    out.push(Number((Math.round(v / step) * step).toPrecision(12)));
  return out;
};

/** Domain padded so a constant series still gets a visible extent. */
export const paddedDomain = (values: number[]): [number, number] => {
  const finite = values.filter(Number.isFinite);
  if (finite.length === 0) return [0, 1];
  const min = Math.min(...finite);
  const max = Math.max(...finite);
  return min === max ? [min - 1, max + 1] : [min, max];
};
