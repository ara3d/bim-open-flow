// The public surface of @bimopenflow/graph: the editor, the view model and
// its geometry helpers, the document helpers, and the canvas theme. Callers
// import from here, never from a module path.

export { createCanvasEditor } from "./canvasEditor.js";
export type { CanvasEditor } from "./canvasEditor.js";
export { anchorId, canConnect, parseAnchorId } from "./canvasIntents.js";
export type { AnchorDir, AnchorRef, CanvasHooks, CanvasIntent } from "./canvasIntents.js";
export {
  buildCanvasModel, defaultPosition, edgeId, freePosition, nodeHeight, nodeWidth, NOTE_KIND,
} from "./viewModel.js";
export type { CanvasEdge, CanvasModel, CanvasNode, CanvasPort, NodeBounds, OpenEditor } from "./viewModel.js";
export { inlineParams, slotControl } from "./canvasSlots.js";
export type { CanvasParam, SlotContext, SlotControl } from "./canvasSlots.js";
export { autoLayout } from "./autoLayout.js";
export { nodeTitle, previewAfterEdit, upstreamIds } from "./graphPreview.js";
export { nodeBadge } from "./nodeBadge.js";
export type { NodeBadge } from "./nodeBadge.js";
export { applyCanvasTheme, canvasThemeNames, currentCanvasTheme, defaultCanvasTheme, isCanvasThemeName } from "./canvasTheme.js";
export type { CanvasThemeName } from "./canvasTheme.js";
export { refreshColumnOptions, setSuggestionProvider } from "./canvasControls.js";
export type { SuggestionProvider } from "./canvasControls.js";
export type { PortResultsView, ReadPort, WireRows } from "./portResults.js";
export { NO_PORT_RESULTS } from "./portResults.js";
