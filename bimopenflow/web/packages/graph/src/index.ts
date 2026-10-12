// The public surface of @bimopenflow/graph: the editor, the view model and
// its geometry helpers, the document helpers, and the canvas theme. Callers
// import from here, never from a module path.

export { createGraphEditor } from "./canvasEditor.js";
export type { CanvasEditorSurfaces, FitOptions, GraphEditor, GraphEditorOptions, Point } from "./canvasEditor.js";
export { createCanvasInstance, pruneInstance } from "./instance.js";
export type { CanvasInstance, SuggestionProvider } from "./instance.js";
export type { FileProvider } from "./filePathControl.js";
export { anchorId, canConnect, MUTATING_INTENTS, parseAnchorId } from "./canvasIntents.js";
export type { AnchorDir, AnchorRef, CanvasHooks, CanvasIntent } from "./canvasIntents.js";
export {
  buildCanvasModel, edgeId, freePosition, newNodeSize,
  NODE_HEADER, NODE_WIDTH, NOTE_KIND, PORT_SPACING, WIDE_NODE_WIDTH,
} from "./viewModel.js";
export type { CanvasEdge, CanvasModel, CanvasNode, CanvasPort, NodeBounds, OpenEditor } from "./viewModel.js";
export { inlineParams, previewText, slotControl } from "./canvasSlots.js";
export { fileName } from "./paramText.js";
export { portY } from "./portGeometry.js";
export type { CanvasParam, SlotContext, SlotControl } from "./canvasSlots.js";
export { autoLayout } from "./autoLayout.js";
export { nodePack, nodeTitle, previewAfterEdit, upstreamIds } from "./graphPreview.js";
export { nodeBadge } from "./nodeBadge.js";
export type { NodeBadge } from "./nodeBadge.js";
export { nodeCardLayout } from "./nodeRender.js";
export type { NodeCardLayout } from "./nodeRender.js";
export { applyCanvasTheme, canvasThemeNames, currentCanvasTheme, defaultCanvasTheme, isCanvasThemeName } from "./canvasTheme.js";
export type { CanvasThemeName } from "./canvasTheme.js";
export type { PortResultsView, ReadPort, WireRows } from "./portResults.js";
export { NO_PORT_RESULTS } from "./portResults.js";
export { DEFAULT_SOURCE_VIEW, isSourceDescriptor, NO_SOURCE_VIEW } from "./sources.js";
export type { SourceChip, SourceView } from "./sources.js";
export { firstFree, freshNodeId } from "./ids.js";
export { dropSourcePlan, dropSourceType, promotePlan, relinkActions, relinkTargets } from "./sourcePlans.js";
export type { RelinkTarget, SourcePlan } from "./sourcePlans.js";
export { findSourceByPath } from "./sources.js";
export { INPUT_KIND, inputCardId, inputUses, inputValue } from "./graphInputs.js";
export type { InputMark } from "./graphInputs.js";
export { canInlineInput, canMakeInput, inlineInputPlan, inputNameFor, makeInputPlan } from "./inputPlans.js";
export type { InputPlan } from "./inputPlans.js";
