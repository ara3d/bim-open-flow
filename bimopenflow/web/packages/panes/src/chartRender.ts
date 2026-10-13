// Draws a chart spec (chartSpec.ts) into an element with vega-embed, loaded on
// first use so a page without charts never fetches Vega. The renderer is
// replaceable for tests (setChartRenderer; @bimopenflow/panes/testing has a
// fake that records the spec instead of drawing it).

import type { ChartSpec } from "./chartSpec";

/** A drawn chart: resize it to its element, or take it down. */
export interface ChartHandle {
  resize(): Promise<void>;
  finalize(): void;
}

export type ChartRenderer = (el: HTMLElement, spec: ChartSpec) => Promise<ChartHandle>;

const vegaRenderer: ChartRenderer = async (el, spec) => {
  const embed = (await import("vega-embed")).default;
  const result = await embed(el, spec as Parameters<typeof embed>[1], { actions: false, renderer: "svg" });
  return {
    resize: async () => { await result.view.resize().runAsync(); },
    finalize: () => result.finalize(),
  };
};

let renderer: ChartRenderer = vegaRenderer;

/** Replaces the renderer; null restores vega-embed. */
export const setChartRenderer = (next: ChartRenderer | null): void => {
  renderer = next ?? vegaRenderer;
};

export const renderChart = (el: HTMLElement, spec: ChartSpec): Promise<ChartHandle> => renderer(el, spec);
