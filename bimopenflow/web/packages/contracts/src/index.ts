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
  | "Json";

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

