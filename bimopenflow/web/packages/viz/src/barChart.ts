import type { TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatNumber, formatValue, numberOf } from "./format";
import {
  columnIndexByName,
  firstNumericColumn,
  firstTextColumn,
  numericColumnIndices,
  resolveColumn,
} from "./columns";
import { linearScale, niceTicks } from "./scale";
import { svgEl } from "./svg";

export interface BarChartOptions {
  width?: number;
  height?: number;
  /** Chart title, rendered above the plot. */
  title?: string;
  /** Text column supplying categories. Default: first Text column. */
  categoryColumn?: string;
  /** Single numeric value column; ignored when seriesColumns is set. */
  valueColumn?: string;
  /**
   * Numeric columns to plot as grouped bars (one bar per series per category).
   * Unknown names are skipped. Default: all numeric columns except the
   * category column (falls back to valueColumn when that is set).
   */
  seriesColumns?: string[];
}

const MARGIN = { top: 16, right: 12, bottom: 28, left: 48 };
const TITLE_H = 20;

const seriesIndices = (
  data: TableData,
  options: BarChartOptions | undefined,
  catIdx: number,
): number[] => {
  if (options?.seriesColumns) {
    const named = options.seriesColumns
      .map((name) => columnIndexByName(data, name))
      .filter((i) => i >= 0 && i !== catIdx);
    if (named.length > 0) return named;
  } else if (options?.valueColumn !== undefined) {
    return [resolveColumn(data, options.valueColumn, firstNumericColumn, "value")];
  }
  const numeric = numericColumnIndices(data).filter((i) => i !== catIdx);
  if (numeric.length === 0)
    throw new Error("bof-viz: no suitable value column in table");
  return numeric;
};

export const BarChart = defineComponent<TableData, BarChartOptions>(
  (root, options) => {
    const width = options?.width ?? 480;
    const height = options?.height ?? 280;

    return (data) => {
      const doc = root.ownerDocument;
      root.textContent = "";
      const catIdx = resolveColumn(data, options?.categoryColumn, firstTextColumn, "category");
      const series = seriesIndices(data, options, catIdx);
      const single = series.length === 1;
      const catType = data.columns[catIdx].type;
      const labels = data.rows.map((r) => formatValue(r[catIdx], catType));

      const plotTop = MARGIN.top + (options?.title ? TITLE_H : 0);
      const plotW = width - MARGIN.left - MARGIN.right;
      const plotH = height - plotTop - MARGIN.bottom;
      const finite = series
        .flatMap((s) => data.rows.map((r) => numberOf(r[s])))
        .filter(Number.isFinite);
      const lo = Math.min(0, ...finite);
      const hi = Math.max(0, ...finite);
      const [min, max] = lo === hi ? [lo, hi + 1] : [lo, hi];
      const y = linearScale(min, max, plotTop + plotH, plotTop);

      const svg = svgEl(doc, "svg", {
        width,
        height,
        viewBox: `0 0 ${width} ${height}`,
        role: "img",
        class: "bof-viz-bar-chart",
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

      for (const t of niceTicks(min, max)) {
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
            {
              class: "bof-viz-tick-label",
              x: MARGIN.left - 6,
              y: y(t) + 3,
              "text-anchor": "end",
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
          y1: y(0),
          y2: y(0),
        }),
      );

      const band = plotW / Math.max(1, data.rows.length);
      const groupW = band * 0.7;
      const barW = single ? groupW : groupW / series.length;
      const y0 = y(0);
      data.rows.forEach((row, i) => {
        const cx = MARGIN.left + i * band + band / 2;
        svg.appendChild(
          svgEl(
            doc,
            "text",
            {
              class: "bof-viz-axis-label",
              x: cx,
              y: plotTop + plotH + 14,
              "text-anchor": "middle",
            },
            labels[i],
          ),
        );
        series.forEach((s, k) => {
          const v = numberOf(row[s]);
          if (!Number.isFinite(v)) return;
          const negative = v < 0;
          const yv = y(v);
          const x = single ? cx - barW / 2 : cx - groupW / 2 + k * barW;
          const attrs: Record<string, string | number> = {
            class: negative ? "bof-viz-bar bof-viz-bar--neg" : "bof-viz-bar",
            x,
            y: Math.min(y0, yv),
            width: barW,
            height: Math.abs(y0 - yv),
            "data-value": formatNumber(v),
          };
          if (!single) {
            attrs.style = `fill: var(--bof-viz-series-${k % 8})`;
            attrs["data-series"] = data.columns[s].name;
          }
          svg.appendChild(svgEl(doc, "rect", attrs));
          if (single)
            svg.appendChild(
              svgEl(
                doc,
                "text",
                {
                  class: "bof-viz-value-label",
                  x: cx,
                  y: negative ? yv + 11 : yv - 4,
                  "text-anchor": "middle",
                },
                formatNumber(v),
              ),
            );
        });
      });

      root.appendChild(svg);
    };
  },
);
