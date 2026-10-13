// The chart theme: fonts, text colours, and the family palette as a Vega-Lite
// `config`, one object so every chart the pane draws agrees with the studio
// (app/src/studio/studio.css) and with BIM Open Plot's demo theme, whose
// palette this copies with the flow accent first. Plot is layered above this
// repository, so the two objects cannot yet be one (TKT-294 notes the debt).

export const chartTokens = {
  accent: "#2f66ce",
  text: "#171a1f",
  dim: "#5a606c",
  surface: "#ffffff",
  border: "#e3e6ea",
  font: '"Public Sans", "Segoe UI", system-ui, sans-serif',
  fontBrand: '"Instrument Sans", "Public Sans", "Segoe UI", system-ui, sans-serif',
} as const;

/** The multi-series and category palette: the flow blue first, then the family's hues. */
export const chartSeries: readonly string[] = [
  "#2f66ce", "#a16207", "#0f8a80", "#5a47c7", "#b8326e", "#d97706", "#4f5d78", "#3b3f47",
];

/** A sequential scheme for quantities: pale ochre to deep brown. */
export const chartHeat: readonly string[] = ["#f6efdb", "#e9cf8a", "#d4a53f", "#a16207", "#6b3f05"];

/** Vega-Lite `config` for every chart the pane draws. */
export const chartConfig = {
  font: chartTokens.font,
  background: chartTokens.surface,
  padding: 8,
  title: {
    color: chartTokens.text, font: chartTokens.fontBrand, fontSize: 14, fontWeight: 600,
    anchor: "start", subtitleColor: chartTokens.dim,
  },
  axis: {
    labelColor: chartTokens.dim, titleColor: chartTokens.text,
    gridColor: chartTokens.border, domainColor: chartTokens.border, tickColor: chartTokens.border,
    labelFontSize: 11, titleFontSize: 12, titleFontWeight: 600, labelLimit: 160,
  },
  legend: {
    labelColor: chartTokens.dim, titleColor: chartTokens.text, labelFontSize: 11, titleFontSize: 12,
    orient: "bottom", direction: "horizontal", columns: 4, symbolType: "square",
  },
  view: { stroke: null },
  range: { category: [...chartSeries], heatmap: [...chartHeat], ramp: [...chartHeat] },
  mark: { color: chartTokens.accent },
  bar: { color: chartTokens.accent, cornerRadiusEnd: 2 },
  line: { color: chartTokens.accent, strokeWidth: 2 },
  point: { color: chartTokens.accent, filled: true, size: 36 },
  text: { color: chartTokens.text, font: chartTokens.font, fontSize: 10 },
} as const;
