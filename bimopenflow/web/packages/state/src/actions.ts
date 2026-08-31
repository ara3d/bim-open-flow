import type { EvalUpdate } from "@bimopenflow/contracts";
import type { NodeLayout } from "./document.js";

export type Action =
  | { type: "addNode"; id: string; kind: string; version: number }
  | { type: "removeNode"; id: string }
  | { type: "connect"; from: string; to: string }
  | { type: "disconnect"; from: string; to: string }
  | { type: "setParam"; nodeId: string; name: string; value: string }
  | { type: "setLayout"; nodeId: string; layout: NodeLayout }
  | { type: "select"; ids: readonly string[] }
  | { type: "clearSelection" }
  | { type: "undo" }
  | { type: "redo" }
  | { type: "applyServerState"; update: EvalUpdate }
  | { type: "setDocument"; json: string }
  | { type: "markSaved" };
