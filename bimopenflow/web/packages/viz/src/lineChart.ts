import type { TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatNumber, isNumericType, numberOf } from "./format";
import { columnIndexByName, seriesColumnIndices } from "./columns";
import { linearScale, niceTicks, paddedDomain } from "./scale";
import { svgEl } from "./svg";

export interface LineChartOptions {
  width?: number;
  height?: number;
  /** Chart title, rendered above the plot. */
  title?: string;
  /**
   * Numeric column for x values. A missing or non-numeric column falls back
   * to the row index (rows arrive pre-sorted). Default: row index.
   */
  xColumn?: string;
  /**
   * Numeric columns to plot. Unknown names are skipped. Default: every
   * numeric column except xColumn.
   */
  seriesColumns?: string[];
}

const MARGIN = { top: 16, right: 12, bottom: 28, left: 48 };
const TITLE_H = 20;

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
      const xNumeric = xIdx >= 0 && isNumericType(data.columns[xIdx].type);
      const series = seriesColumnIndices(data, options?.seriesColumns, xIdx);
      const xs = data.rows.map((r, i) => (xNumeric ? numberOf(r[xIdx]) : i));
      const allYs = series.flatMap((s) => data.rows.map((r) => numberOf(r[s])));

      const plotTop = MARGIN.top + (options?.title ? TITLE_H : 0);
      const plotW = width - MARGIN.left - MARGIN.right;
      const plotH = height - plotTop - MARGIN.bottom;
      const [x0, x1] = paddedDomain(xs);
      const [y0, y1] = paddedDomain(allYs);
      const x = linearScale(x0, x1, MARGIN.left, MARGIN.left + plotW);
      const y = linearScale(y0, y1, plotTop + plotH, plotTop);

      const svg = svgEl(doc, "svg", {
        width,
        height,
        viewBox: `0 0 ${width} ${height}`,
        role: "img",
        class: "bof-viz-line-chart",
      });

      if (options?.title)
        svg.appendChild(
          svgEl(
            doc,
            "text",
            {
              class: "bof-viz-title",
              x: width / 2,
              y: MARGIN.top,
              "text-anchor": "middle",
              fill: "var(--bof-viz-fg)",
              "font-size": 13,
              "font-weight": 600,
            },
            options.title,
          ),
        );

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
            y1: plotTop + plotH,
            y2: plotTop + plotH + 4,
          }),
        );
        svg.appendChild(
          svgEl(
            doc,
            "text",
            {
              class: "bof-viz-tick-label",
              x: x(t),
              y: plotTop + plotH + 14,
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
          y1: plotTop,
          y2: plotTop + plotH,
        }),
      );
      svg.appendChild(
        svgEl(doc, "line", {
          class: "bof-viz-axis-line",
          x1: MARGIN.left,
          x2: MARGIN.left + plotW,
          y1: plotTop + plotH,
          y2: plotTop + plotH,
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
