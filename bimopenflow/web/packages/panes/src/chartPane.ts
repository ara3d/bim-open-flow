import type { TableSlice } from "@bimopenflow/contracts";
import type { Pane } from "./pane";
import { definePane } from "./base";
import { chartSpec, splitRowColors, type ChartPaneOptions } from "./chartSpec";
import { renderChart, type ChartHandle } from "./chartRender";
import { parseScaleLegend, renderLegendView, scaleLegendView } from "./scaleLegend";

export type { BarChartOptions, BarColorBy, BarLayout, ChartPaneOptions, LineChartOptions } from "./chartSpec";
export { splitRowColors } from "./chartSpec";

/**
 * Chart pane: draws the chart.bar or chart.line spec (chartSpec.ts) of its
 * options over each "table" input, filling the pane (the spec's width and
 * height are the container's, and a ResizeObserver redraws on a resize) so
 * the pane never scrolls for a chart that fits. A table's r/g/b columns, if
 * present, colour the bars and are dropped from the plotted data. A "legend"
 * input draws the shared strip below the chart, the same way the 3D pane
 * does; an empty legend hides it.
 * Inputs: "table" renders/updates the chart; "legend" draws the strip;
 * everything else is ignored. Emits no events.
 */
export const createChartPane = (options: ChartPaneOptions): Pane =>
  definePane((root) => {
    root.classList.add("bof-panes-chartpane");
    const chartEl = root.ownerDocument.createElement("div");
    chartEl.className = "bof-panes-chart";
    root.append(chartEl);
    const legend = root.ownerDocument.createElement("div");
    legend.className = "bof-panes-legend";
    legend.setAttribute("aria-label", "Scale legend");
    legend.hidden = true;
    root.append(legend);

    let handle: ChartHandle | null = null;
    let lastSpec = "";
    let token = 0;

    const draw = (data: TableSlice) => {
      const { data: plotted, colors } = splitRowColors(data);
      const spec = chartSpec(plotted, options, colors);
      const key = JSON.stringify(spec);
      if (key === lastSpec) return;
      lastSpec = key;
      const mine = ++token;
      void renderChart(chartEl, spec).then((next) => {
        if (mine !== token) { next.finalize(); return; }
        handle?.finalize();
        handle = next;
      });
    };

    const observer = typeof ResizeObserver === "undefined"
      ? null
      : new ResizeObserver(() => { void handle?.resize(); });
    observer?.observe(chartEl);

    const showLegend = (data: TableSlice) => {
      const parsed = parseScaleLegend(data);
      if (!parsed) {
        legend.replaceChildren();
        legend.hidden = true;
        return;
      }
      renderLegendView(legend, scaleLegendView(parsed));
    };

    return {
      update(input) {
        if (input.kind === "table") draw(input.data);
        else if (input.kind === "legend") showLegend(input.data);
      },
      destroy() {
        token++;
        observer?.disconnect();
        handle?.finalize();
        handle = null;
      },
    };
  });
