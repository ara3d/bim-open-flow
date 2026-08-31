import type { TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatNumber, numberOf } from "./format";
import { columnIndexByName, numericColumnIndices } from "./columns";
import { linearScale, niceTicks, paddedDomain } from "./scale";
import { svgEl } from "./svg";

export interface LineChartOptions {
  width?: number;
  height?: number;
  /** Numeric column for x values. Default: row index. */
  xColumn?: string;
  /** Numeric columns to plot. Default: every numeric column except xColumn. */
  seriesColumns?: string[];
}

const MARGIN = { top: 16, right: 12, bottom: 28, left: 48 };

const seriesIndices = (data: TableData, options: LineChartOptions | undefined, xIdx: number): number[] => {
  if (options?.seriesColumns) {
    return options.seriesColumns.map((name) => {
      const i = columnIndexByName(data, name);
      if (i < 0) throw new Error(`bof-viz: series column "${name}" not found`);
      return i;
    });
  }
  return numericColumnIndices(data).filter((i) => i !== xIdx);
};

/** Path with a break (new moveto) at every non-finite point. */
const pathFor = (
  xs: number[],
  ys: number[],
  x: (v: number) => number,
  y: (v: number) => number,
): string => {
  let d = "";
  let pen = false;
  for (let i = 0; i < xs.length; i++) {
    if (!Number.isFinite(xs[i]) || !Number.isFinite(ys[i])) {
      pen = false;
      continue;
    }
    d += `${pen ? "L" : "M"}${x(xs[i])},${y(ys[i])}`;
    pen = true;
  }
  return d;
};

export const LineChart = defineComponent<TableData, LineChartOptions>(
  (root, options) => {
    const width = options?.width ?? 480;
    const height = options?.height ?? 280;

    return (data) => {
      const doc = root.ownerDocument;
      root.textContent = "";
      const xIdx =
        options?.xColumn !== undefined
          ? columnIndexByName(data, options.xColumn)
          : -1;
      if (options?.xColumn !== undefined && xIdx < 0)
        throw new Error(`bof-viz: x column "${options.xColumn}" not found`);
      const series = seriesIndices(data, options, xIdx);
      const xs = data.rows.map((r, i) => (xIdx >= 0 ? numberOf(r[xIdx]) : i));
      const allYs = series.flatMap((s) => data.rows.map((r) => numberOf(r[s])));

      const plotW = width - MARGIN.left - MARGIN.right;
      const plotH = height - MARGIN.top - MARGIN.bottom;
      const [x0, x1] = paddedDomain(xs);
      const [y0, y1] = paddedDomain(allYs);
      const x = linearScale(x0, x1, MARGIN.left, MARGIN.left + plotW);
      const y = linearScale(y0, y1, MARGIN.top + plotH, MARGIN.top);

      const svg = svgEl(doc, "svg", {
        width,
        height,
        viewBox: `0 0 ${width} ${height}`,
        role: "img",
        class: "bof-viz-line-chart",
      });

      for (const t of niceTicks(y0, y1)) {
        svg.appendChild(
          svgEl(doc, "line", {
            class: "bof-viz-tick",
            x1: MARGIN.left - 4,
            x2: MARGIN.left,
            y1: y(t),
            y2: y(t),
          }),
        );
        svg.appendChild(
          svgEl(
            doc,
            "text",
            { class: "bof-viz-tick-label", x: MARGIN.left - 6, y: y(t) + 3, "text-anchor": "end" },
            formatNumber(t),
          ),
        );
      }
      for (const t of niceTicks(x0, x1)) {
        svg.appendChild(
          svgEl(doc, "line", {
            class: "bof-viz-tick",
            x1: x(t),
            x2: x(t),
            y1: MARGIN.top + plotH,
            y2: MARGIN.top + plotH + 4,
          }),
        );
        svg.appendChild(
          svgEl(
            doc,
            "text",
            {
              class: "bof-viz-tick-label",
              x: x(t),
              y: MARGIN.top + plotH + 14,
              "text-anchor": "middle",
            },
            formatNumber(t),
          ),
        );
      }
      svg.appendChild(
        svgEl(doc, "line", {
          class: "bof-viz-axis-line",
          x1: MARGIN.left,
          x2: MARGIN.left,
          y1: MARGIN.top,
          y2: MARGIN.top + plotH,
        }),
      );
      svg.appendChild(
        svgEl(doc, "line", {
          class: "bof-viz-axis-line",
          x1: MARGIN.left,
          x2: MARGIN.left + plotW,
          y1: MARGIN.top + plotH,
          y2: MARGIN.top + plotH,
        }),
      );

      series.forEach((s, i) => {
        const ys = data.rows.map((r) => numberOf(r[s]));
        svg.appendChild(
          svgEl(doc, "path", {
            class: "bof-viz-line",
            d: pathFor(xs, ys, x, y),
            stroke: `var(--bof-viz-series-${i % 8})`,
            "data-series": data.columns[s].name,
          }),
        );
      });

      root.appendChild(svg);
    };
  },
);
