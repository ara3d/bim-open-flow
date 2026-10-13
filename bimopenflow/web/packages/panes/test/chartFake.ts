// A chart renderer for tests: instead of drawing with Vega, it writes the spec
// into the element (`data-spec`, with `data-mark` naming the first mark) so a
// test reads what the pane would have drawn without a layout engine.
import type { ChartHandle, ChartSpec } from "../src";
import { setChartRenderer } from "../src";

export interface FakeChart {
  /** Every spec rendered, in order. */
  readonly specs: ChartSpec[];
  /** How many drawn charts were finalized. */
  finalized: number;
  resized: number;
  /** Restores vega-embed. */
  restore(): void;
}

const firstMark = (spec: ChartSpec): string => {
  const layer = spec.layer as { mark?: { type?: string } }[] | undefined;
  const mark = (layer?.[0]?.mark ?? spec.mark) as { type?: string } | undefined;
  return mark?.type ?? "";
};

/** Installs the fake renderer until `restore`. */
export const fakeChartRenderer = (): FakeChart => {
  const fake: FakeChart = {
    specs: [],
    finalized: 0,
    resized: 0,
    restore: () => setChartRenderer(null),
  };
  setChartRenderer(async (el, spec) => {
    fake.specs.push(spec);
    el.dataset.spec = JSON.stringify(spec);
    el.dataset.mark = firstMark(spec);
    const handle: ChartHandle = {
      resize: async () => { fake.resized++; },
      finalize: () => { fake.finalized++; },
    };
    return handle;
  });
  return fake;
};

/** The spec an element holds after the fake drew it. */
export const drawnSpec = (host: ParentNode): ChartSpec | null => {
  const el = host.querySelector<HTMLElement>(".bof-panes-chart");
  return el?.dataset.spec ? (JSON.parse(el.dataset.spec) as ChartSpec) : null;
};

/** The first layer's encoding, or the spec's own. */
export const drawnEncoding = (spec: ChartSpec): Record<string, { field?: string } & Record<string, unknown>> => {
  const layer = spec.layer as { encoding: Record<string, never> }[] | undefined;
  return (layer?.[0]?.encoding ?? spec.encoding) as Record<string, { field?: string } & Record<string, unknown>>;
};
