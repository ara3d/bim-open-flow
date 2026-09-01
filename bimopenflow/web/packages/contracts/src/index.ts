// Generated from contracts/contracts.json v0.1.0 by contracts/generate.mjs.
// Do not edit by hand.

export type ParamKind =
  | "Boolean"
  | "Integer"
  | "Number"
  | "Text"
  | "Enum"
  | "FilePath"
  | "ModelRef"
  | "Expression"
  | "Json"
  | "DateTime";

export type PortType =
  | "Boolean"
  | "Integer"
  | "Number"
  | "Text"
  | "Table"
  | "Any";

export type ColumnType =
  | "Boolean"
  | "Integer"
  | "Number"
  | "Text";

export type NodeCapability =
  | "Pure"
  | "Effect";

export type Verdict =
  | "Pass"
  | "Fail"
  | "NeedsReview"
  | "InfoNotAvailable";

export type ModelKind =
  | "Ifc"
  | "Bos";

export type NodeStatus =
  | "Ok"
  | "Unready"
  | "EffectPending"
  | "Unavailable"
  | "Error";

export interface ColumnSchema {
  name: string;
  type: ColumnType;
}

export interface TableData {
  columns: ColumnSchema[];
  rows: unknown[][];
}

export interface PortDescriptor {
  name: string;
  type: PortType;
  optional: boolean;
}

export interface ParamDescriptor {
  name: string;
  kind: ParamKind;
  default: string;
  enumValues?: string[] | undefined;
}

export interface NodeDescriptor {
  kind: string;
  version: number;
  capability: NodeCapability;
  inputs: PortDescriptor[];
  outputs: PortDescriptor[];
  params: ParamDescriptor[];
  description: string;
}

export interface NodeCatalog {
  nodes: NodeDescriptor[];
}

export interface ModelSummary {
  id: string;
  name: string;
  kind: ModelKind;
  sizeBytes: number;
  lastWriteUtc: string;
}

export interface AnalysisSummary {
  id: string;
  graphHash: string;
}

export interface AnalysisVersion {
  version: number;
  graphHash: string;
}

export interface NodeState {
  nodeId: string;
  status: NodeStatus;
  error?: string | undefined;
  warnings: string[];
}

export interface EvalUpdate {
  analysisId: string;
  nodes: NodeState[];
}

export interface TableSlice {
  columns: ColumnSchema[];
  rows: unknown[][];
  totalRows: number;
  skip: number;
}

export interface RunSummary {
  fileName: string;
  timestampUtc: string;
  graphHash: string;
}

export interface SelectionEvent {
  source: string;
  ids: string[];
}

export interface ApiError {
  error: string;
}

