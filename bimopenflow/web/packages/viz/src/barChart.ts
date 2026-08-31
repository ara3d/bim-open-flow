import type { TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatNumber, formatValue, numberOf } from "./format";
import { firstNumericColumn, firstTextColumn, resolveColumn } from "./columns";
import { linearScale, niceTicks } from "./scale";
import { svgEl } from "./svg";

export interface BarChartOptions {
  width?: number;
  height?: number;
  /** Text column supplying categories. Default: first Text column. */
  categoryColumn?: string;
  /** Numeric column supplying values. Default: first Integer/Number column. */
  valueColumn?: string;
}

const MARGIN = { top: 16, right: 12, bottom: 28, left: 48 };

export const BarChart = defineComponent<TableData, BarChartOptions>(
  (root, options) => {
    const width = options?.width ?? 480;
    const height = options?.height ?? 280;

    return (data) => {
      const doc = root.ownerDocument;
      root.textContent = "";
      const catIdx = resolveColumn(data, options?.categoryColumn, firstTextColumn, "category");
      const valIdx = resolveColumn(data, options?.valueColumn, firstNumericColumn, "value");
      const catType = data.columns[catIdx].type;
      const values = data.rows.map((r) => numberOf(r[valIdx]));
      const labels = data.rows.map((r) => formatValue(r[catIdx], catType));

      const plotW = width - MARGIN.left - MARGIN.right;
      const plotH = height - MARGIN.top - MARGIN.bottom;
      const finite = values.filter(Number.isFinite);
      const lo = Math.min(0, ...finite);
      const hi = Math.max(0, ...finite);
      const [min, max] = lo === hi ? [lo, hi + 1] : [lo, hi];
      const y = linearScale(min, max, MARGIN.top + plotH, MARGIN.top);

      const svg = svgEl(doc, "svg", {
        width,
        height,
        viewBox: `0 0 ${width} ${height}`,
        role: "img",
        class: "bof-viz-bar-chart",
      });

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
          y1: MARGIN.top,
          y2: MARGIN.top + plotH,
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

      const band = plotW / Math.max(1, values.length);
      const barW = band * 0.7;
      values.forEach((v, i) => {
        const cx = MARGIN.left + i * band + band / 2;
        const label = svgEl(
          doc,
          "text",
          {
            class: "bof-viz-axis-label",
            x: cx,
            y: MARGIN.top + plotH + 14,
            "text-anchor": "middle",
          },
          labels[i],
        );
        svg.appendChild(label);
        if (!Number.isFinite(v)) return;
        const negative = v < 0;
        const y0 = y(0);
        const yv = y(v);
        svg.appendChild(
          svgEl(doc, "rect", {
            class: negative ? "bof-viz-bar bof-viz-bar--neg" : "bof-viz-bar",
            x: cx - barW / 2,
            y: Math.min(y0, yv),
            width: barW,
            height: Math.abs(y0 - yv),
            "data-value": formatNumber(v),
          }),
        );
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

      root.appendChild(svg);
    };
  },
);
