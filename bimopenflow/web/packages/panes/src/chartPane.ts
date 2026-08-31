import type { TableSlice } from "@bimopenflow/contracts";
import {
  BarChart,
  LineChart,
  type BarChartOptions,
  type LineChartOptions,
} from "@bimopenflow/viz";
import type { Pane } from "./pane";
import { definePane } from "./base";

/** Chart choice plus the chosen chart's viz options, passed through as-is. */
export type ChartPaneOptions =
  | ({ chart: "bar" } & BarChartOptions)
  | ({ chart: "line" } & LineChartOptions);

/**
 * Chart pane: wraps the viz BarChart or LineChart.
 * Inputs: "table" renders/updates the chart; everything else is ignored.
 * Emits no events.
 */
export const createChartPane = (options: ChartPaneOptions): Pane =>
  definePane((root) => {
    let handle: { update(data: TableSlice): void; destroy(): void } | null =
      null;
    const mountChart = (data: TableSlice) =>
      options.chart === "bar"
        ? BarChart.mount(root, data, options)
        : LineChart.mount(root, data, options);
    return {
      update(input) {
        if (input.kind !== "table") return;
        if (handle) handle.update(input.data);
        else handle = mountChart(input.data);
      },
      destroy() {
        handle?.destroy();
      },
    };
  });
