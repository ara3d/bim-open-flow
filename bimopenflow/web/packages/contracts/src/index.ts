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
  | "DateTime"
  | "Fraction"
  | "Percent";

export type PortType =
  | "Boolean"
  | "Integer"
  | "Number"
  | "Text"
  | "Table"
  | "Relation"
  | "Database"
  | "Any";

export type ColumnType =
  | "Boolean"
  | "Integer"
  | "Number"
  | "Text"
  | "Date"
  | "Timestamp"
  | "Binary"
  | "Unknown";

export type SchemaMode =
  | "Static"
  | "Inferred"
  | "Dynamic";

export type SchemaStatus =
  | "Ok"
  | "Unknown"
  | "Error"
  | "Dynamic";

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

export type SuggestKind =
  | "ColumnsOfInput"
  | "TablesInFile"
  | "FilesUnderRoots";

export type ColumnUse =
  | "Any"
  | "Value"
  | "Label"
  | "Key"
  | "JoinKey"
  | "Aggregate";

export type SuggestStatus =
  | "Ok"
  | "Unready"
  | "Unavailable";

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
  suggest?: SuggestDescriptor | undefined;
  control?: ControlDescriptor | undefined;
  bindable?: PortType | undefined;
  list?: boolean | undefined;
}

export interface ControlDescriptor {
  kind: string;
  min?: number | undefined;
  max?: number | undefined;
  step?: number | undefined;
  unit?: string | undefined;
  label?: string | undefined;
}

export interface SuggestDescriptor {
  kind: SuggestKind;
  source: string;
  use?: ColumnUse | undefined;
  sharedWith?: string | undefined;
}

export interface Suggestion {
  value: string;
  detail?: string | undefined;
  kind?: string | undefined;
  meaning?: string | undefined;
  role?: string | undefined;
  functions?: string[] | undefined;
}

export interface SuggestionList {
  status: SuggestStatus;
  values: Suggestion[];
  reason?: string | undefined;
}

export interface FolderEntry {
  name: string;
  path: string;
  isDirectory: boolean;
  sizeBytes?: number | undefined;
}

export interface FolderListing {
  dir: string;
  parent?: string | undefined;
  roots: string[];
  entries: FolderEntry[];
}

export interface PathStatus {
  path: string;
  exists: boolean;
  root?: string | undefined;
  relative?: string | undefined;
}

export interface SchemaColumn {
  name: string;
  type: ColumnType;
  nullable: boolean;
  kind?: string | undefined;
}

export interface OutputSchema {
  output: string;
  mode: SchemaMode;
  columns?: SchemaColumn[] | undefined;
}

export interface PortSchema {
  node: string;
  output: string;
  status: SchemaStatus;
  columns?: SchemaColumn[] | undefined;
  reason?: string | undefined;
  errors: string[];
  warnings: string[];
}

export interface AnalysisSchemas {
  analysisId: string;
  graphHash: string;
  ports: PortSchema[];
}

export interface NodeDescriptor {
  kind: string;
  version: number;
  capability: NodeCapability;
  inputs: PortDescriptor[];
  outputs: PortDescriptor[];
  params: ParamDescriptor[];
  description: string;
  schemas?: OutputSchema[] | undefined;
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
  sourcePath: string;
}

export interface EntityParameter {
  group: string;
  name: string;
  value: string;
  units?: string | undefined;
}

export interface EntityProperties {
  localId: number;
  globalId?: string | undefined;
  name?: string | undefined;
  category?: string | undefined;
  parameters: EntityParameter[];
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

export interface GraphInputInfo {
  name: string;
  kind: ParamKind;
  default: string;
  description: string;
  value: string;
}

export interface EvalUpdate {
  analysisId: string;
  nodes: NodeState[];
  graphHash?: string | undefined;
  graphInputs?: GraphInputInfo[] | undefined;
  schemas?: PortSchema[] | undefined;
}

export interface EditorSession {
  analysisId?: string | undefined;
  selection: string[];
  updatedUtc?: string | undefined;
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

