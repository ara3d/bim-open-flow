import type { NodeState } from "@bimopenflow/contracts";
import type { Action } from "./actions.js";
import {
  emptyDocument,
  parseDocument,
  parsePortRef,
  serializeDocument,
  type GraphDocument,
  type NodeLayout,
} from "./document.js";

export interface State {
  readonly document: GraphDocument;
  readonly selection: readonly string[];
  readonly evalState: Readonly<Record<string, NodeState>>;
  readonly dirty: boolean;
  /** Undo history: serialized document snapshots, oldest first. */
  readonly undoStack: readonly string[];
  readonly redoStack: readonly string[];
}

export const initialState: State = {
  document: emptyDocument,
  selection: [],
  evalState: {},
  dirty: false,
  undoStack: [],
  redoStack: [],
};

function findNode(doc: GraphDocument, id: string) {
  return doc.structure.nodes.find((n) => n.id === id);
}

function requireNode(doc: GraphDocument, id: string): void {
  if (!findNode(doc, id)) throw new Error(`No node with id '${id}'`);
}

function without<T>(record: Readonly<Record<string, T>>, key: string): Record<string, T> {
  return Object.fromEntries(Object.entries(record).filter(([k]) => k !== key));
}

/** A document edit: snapshots the current document for undo and clears redo. */
function edited(state: State, next: GraphDocument): State {
  return {
    ...state,
    document: next,
    undoStack: [...state.undoStack, serializeDocument(state.document)],
    redoStack: [],
    dirty: true,
  };
}

function addNode(doc: GraphDocument, id: string, kind: string, version: number): GraphDocument {
  if (id.length === 0 || id.includes("."))
    throw new Error(`Invalid node id '${id}': must be non-empty and contain no dot`);
  if (findNode(doc, id)) throw new Error(`Node id '${id}' already exists`);
  return { ...doc, structure: { ...doc.structure, nodes: [...doc.structure.nodes, { id, kind, version }] } };
}

/** Removes the node and everything that hangs off it: its edges, values, and layout. */
function removeNode(doc: GraphDocument, id: string): GraphDocument {
  requireNode(doc, id);
  return {
    ...doc,
    structure: {
      nodes: doc.structure.nodes.filter((n) => n.id !== id),
      edges: doc.structure.edges.filter(
        (e) => parsePortRef(e.from).nodeId !== id && parsePortRef(e.to).nodeId !== id),
    },
    values: without(doc.values, id),
    layout: without(doc.layout, id),
  };
}

/** An input port takes at most one edge, so any existing edge into 'to' is replaced. */
function connect(doc: GraphDocument, from: string, to: string): GraphDocument {
  parsePortRef(from);
  parsePortRef(to);
  return {
    ...doc,
    structure: {
      ...doc.structure,
      edges: [...doc.structure.edges.filter((e) => e.to !== to), { from, to }],
    },
  };
}

function disconnect(doc: GraphDocument, from: string, to: string): GraphDocument {
  if (!doc.structure.edges.some((e) => e.from === from && e.to === to))
    throw new Error(`No edge '${from}' -> '${to}'`);
  return {
    ...doc,
    structure: {
      ...doc.structure,
      edges: doc.structure.edges.filter((e) => e.from !== from || e.to !== to),
    },
  };
}

function setParam(doc: GraphDocument, nodeId: string, name: string, value: string): GraphDocument {
  requireNode(doc, nodeId);
  return { ...doc, values: { ...doc.values, [nodeId]: { ...doc.values[nodeId], [name]: value } } };
}

function setLayout(doc: GraphDocument, nodeId: string, layout: NodeLayout): GraphDocument {
  requireNode(doc, nodeId);
  return { ...doc, layout: { ...doc.layout, [nodeId]: layout } };
}

export function reduce(state: State, action: Action): State {
  switch (action.type) {
    case "addNode":
      return edited(state, addNode(state.document, action.id, action.kind, action.version));
    case "removeNode":
      return edited(state, removeNode(state.document, action.id));
    case "connect":
      return edited(state, connect(state.document, action.from, action.to));
    case "disconnect":
      return edited(state, disconnect(state.document, action.from, action.to));
    case "setParam":
      return edited(state, setParam(state.document, action.nodeId, action.name, action.value));
    case "setLayout":
      return edited(state, setLayout(state.document, action.nodeId, action.layout));
    case "select":
      return { ...state, selection: [...action.ids] };
    case "clearSelection":
      return { ...state, selection: [] };
    case "undo":
      return state.undoStack.length === 0
        ? state
        : {
            ...state,
            document: parseDocument(state.undoStack[state.undoStack.length - 1]!),
            undoStack: state.undoStack.slice(0, -1),
            redoStack: [...state.redoStack, serializeDocument(state.document)],
            dirty: true,
          };
    case "redo":
      return state.redoStack.length === 0
        ? state
        : {
            ...state,
            document: parseDocument(state.redoStack[state.redoStack.length - 1]!),
            redoStack: state.redoStack.slice(0, -1),
            undoStack: [...state.undoStack, serializeDocument(state.document)],
            dirty: true,
          };
    case "applyServerState":
      return {
        ...state,
        evalState: {
          ...state.evalState,
          ...Object.fromEntries(action.update.nodes.map((n) => [n.nodeId, n])),
        },
      };
    case "setDocument":
      return {
        ...initialState,
        document: parseDocument(action.json),
      };
    case "markSaved":
      return { ...state, dirty: false };
  }
}
