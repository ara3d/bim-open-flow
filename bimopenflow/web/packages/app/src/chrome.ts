// The seam between the controller (app.ts) and a look. A chrome is the frame
// around the shared content: the page layout, the command bar, the flow
// picker, the node catalog, and any toolbar. The controller mounts the shared
// content (the canvas, the step list, the problems strip, the start page, the
// Ask panel, the pane area) into the slots a chrome provides and drives the
// chrome through AppChrome; the chrome talks back only through ChromeActions.
// Two looks exist today, classicChrome.ts and studio/studioChrome.ts, and a
// third is one more factory. The controller never reads a chrome's DOM.

import type { AnalysisSummary, NodeDescriptor } from "@bimopenflow/contracts";
import type { CanvasThemeName, NodeStyleName } from "@bimopenflow/graph";
import type { HostStatus } from "./hostStatus.js";

/** What a chrome may ask the controller to do. Every call is fire-and-forget;
 *  failures surface as toasts, never as return values. */
export interface ChromeActions {
  openAnalysis(id: string): void;
  /** "New": the start page, whose cards open, copy, or create a blank flow. */
  newAnalysis(): void;
  save(): void;
  run(): void;
  setTheme(name: CanvasThemeName): void;
  setNodeStyle(name: NodeStyleName): void;
  /** Adds the node at the first free spot, selected, as one undo step. */
  addNode(desc: NodeDescriptor): void;
  /** Selects the node and brings it into view (the step list's gesture). */
  selectAndFocus(nodeId: string): void;
  /** An explicit choice of what the pane shows (TKT-113): the answer goes
   *  back to following the flow, any other node is shown as an override. */
  showInPane(nodeId: string): void;
  fit(): void;
  /** Lays every node out left to right in dataflow order, then fits. */
  tidy(): void;
}

/** One node of the open flow, as a chrome lists it. */
export interface ChromeNode {
  readonly id: string;
  readonly title: string;
}

/** The DOM the controller mounts shared content into. */
export interface ChromeSlots {
  readonly canvas: HTMLCanvasElement;
  /** The positioned box around the canvas; the start page and the problems
   *  strip overlay it and nothing else. */
  readonly canvasHost: HTMLElement;
  readonly paneEl: HTMLElement;
  /** Empty (taking no room) until the Ask panel mounts into it. */
  readonly askHost: HTMLElement;
  /** Where the step list renders. */
  readonly stepsEl: HTMLElement;
}

export interface AppChrome extends ChromeSlots {
  setAnalyses(list: readonly AnalysisSummary[], activeId: string | null): void;
  setDirty(dirty: boolean): void;
  setConnection(status: HostStatus): void;
  /** Shows the choice without calling the action. */
  setTheme(name: CanvasThemeName): void;
  setNodeStyle(name: NodeStyleName): void;
  setCatalog(nodes: readonly NodeDescriptor[]): void;
  /** A flow opened; `hasNodes` lets an empty flow lead with the catalog. */
  flowOpened(hasNodes: boolean): void;
  /** The open flow's nodes changed (a chrome with a node picker or an
   *  empty-canvas state follows this). */
  setNodes(nodes: readonly ChromeNode[]): void;
  /** Which node the pane area shows now, or null for none. */
  setShownNode(nodeId: string | null): void;
  dispose(): void;
}

export type ChromeFactory = (root: HTMLElement, actions: ChromeActions) => AppChrome;
