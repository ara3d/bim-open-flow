import type { TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatNumber, formatValue, numberOf } from "./format";
import {
  columnIndexByName,
  firstTextColumn,
  seriesColumnIndices,
} from "./columns";
import { linearScale, niceTicks } from "./scale";
import { svgEl } from "./svg";

export interface BarChartOptions {
  width?: number;
  height?: number;
  /** Chart title, rendered above the plot. */
  title?: string;
  /**
   * Text column supplying categories. Unknown names fall back to the first
   * Text column; without one, bars are labeled by row index. Default: first
   * Text column.
   */
  categoryColumn?: string;
  /** Single numeric value column; ignored when seriesColumns is set. */
  valueColumn?: string;
  /**
   * Numeric columns to plot as grouped bars (one bar per series per category).
   * Unknown names are skipped. Default: all numeric columns except the
   * category column (falls back to valueColumn when that is set).
   */
  seriesColumns?: string[];
  /** CSS fill per row for single-series charts; null or missing entries keep the default. Ignored with several series. */
  barColors?: readonly (string | null)[];
}

const MARGIN = { top: 16, right: 12, bottom: 28, left: 48 };
const TITLE_H = 20;
/** Average glyph width of the 11px axis labels, for fitting labels to bars. */
const LABEL_CHAR_W = 6;
/** Longest drop below the axis a slanted label may take; longer ones overflow. */
const MAX_SLANT_DROP = 120;

/**
 * Category label layout: level when the longest label fits its bar's band,
 * else slanted 45 degrees, with the plot shortened by the labels' drop and
 * the left margin widened so the first label stays inside the chart.
 */
const labelLayout = (labels: readonly string[], width: number) => {
  const longest = Math.max(0, ...labels.map((l) => l.length * LABEL_CHAR_W));
  const band = (width - MARGIN.left - MARGIN.right) / Math.max(1, labels.length);
  if (longest <= band - 2) return { slant: false, drop: 0, left: MARGIN.left };
  const drop = Math.min(Math.ceil(longest * Math.SQRT1_2), MAX_SLANT_DROP);
  return { slant: true, drop, left: Math.max(MARGIN.left, Math.ceil(drop - band / 2)) };
};

const seriesIndices = (
  data: TableData,
  options: BarChartOptions | undefined,
  catIdx: number,
): number[] => {
  if (!options?.seriesColumns && options?.valueColumn !== undefined) {
    const i = columnIndexByName(data, options.valueColumn);
    if (i >= 0) return [i];
  }
  return seriesColumnIndices(data, options?.seriesColumns, catIdx);
};

/** Named category, else first Text column; -1 means "use row-index labels". */
const categoryIndex = (data: TableData, name: string | undefined): number => {
  if (name !== undefined) {
    const i = columnIndexByName(data, name);
    if (i >= 0) return i;
  }
  return firstTextColumn(data);
};

export const BarChart = defineComponent<TableData, BarChartOptions>(
  (root, options) => {
    const width = options?.width ?? 480;
    const height = options?.height ?? 280;

    return (data) => {
      const doc = root.ownerDocument;
      root.textContent = "";
      const catIdx = categoryIndex(data, options?.categoryColumn);
      const series = seriesIndices(data, options, catIdx);
      const single = series.length === 1;
      const labels =
        catIdx >= 0
          ? data.rows.map((r) => formatValue(r[catIdx], data.columns[catIdx].type))
          : data.rows.map((_, i) => String(i + 1));

      const layout = labelLayout(labels, width);
      const left = layout.left;
      const plotTop = MARGIN.top + (options?.title ? TITLE_H : 0);
      const plotW = width - left - MARGIN.right;
      const plotH = height - plotTop - MARGIN.bottom - layout.drop;
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

      // TODO: hoist this title block (duplicated in lineChart.ts) once svg.ts
      // is open for edits; it is frozen this wave.
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
            x1: left - 4,
            x2: left,
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
              x: left - 6,
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
          x1: left,
          x2: left,
          y1: plotTop,
          y2: plotTop + plotH,
        }),
      );
      svg.appendChild(
        svgEl(doc, "line", {
          class: "bof-viz-axis-line",
          x1: left,
          x2: left + plotW,
          y1: y(0),
          y2: y(0),
        }),
      );

      const band = plotW / Math.max(1, data.rows.length);
      const groupW = band * 0.7;
      const barW = single ? groupW : groupW / series.length;
      const y0 = y(0);
      data.rows.forEach((row, i) => {
        const cx = left + i * band + band / 2;
        const ly = plotTop + plotH + (layout.slant ? 10 : 14);
        svg.appendChild(
          svgEl(
            doc,
            "text",
            layout.slant
              ? {
                  class: "bof-viz-axis-label",
                  x: cx,
                  y: ly,
                  "text-anchor": "end",
                  transform: `rotate(-45 ${cx} ${ly})`,
                }
              : { class: "bof-viz-axis-label", x: cx, y: ly, "text-anchor": "middle" },
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
          } else {
            const barColor = options?.barColors?.[i];
            if (barColor) attrs.style = `fill: ${barColor}`;
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
