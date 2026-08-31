// Pure grouping logic for verdict tables (the convention defined in
// src/BimOpenFlow.Nodes.Compliance/README.md): columns verdict, checkId,
// checkTitle, citation, matched by exact name.
import type { ColumnSchema, Verdict } from "@bimopenflow/contracts";
import { columnIndex } from "./columns";

export const VERDICTS: readonly Verdict[] = [
  "Pass",
  "Fail",
  "NeedsReview",
  "InfoNotAvailable",
];

/** Severity order per the compliance convention: Fail worst, Pass best. */
export const severityRank = (v: Verdict): number =>
  v === "Fail" ? 3 : v === "NeedsReview" ? 2 : v === "InfoNotAvailable" ? 1 : 0;

export type VerdictCounts = Record<Verdict, number>;

export interface CheckGroup {
  readonly checkId: string;
  readonly checkTitle: string;
  readonly citation: string;
  readonly counts: VerdictCounts;
  /** Most severe verdict present in the group. */
  readonly worst: Verdict;
  /** Row indices (into the input rows) belonging to this check. */
  readonly rowIndices: readonly number[];
}

const REQUIRED = ["verdict", "checkId", "checkTitle", "citation"] as const;

const isVerdict = (v: unknown): v is Verdict =>
  VERDICTS.includes(v as Verdict);

/**
 * Groups a verdict table by checkId, in first-appearance order, counting
 * verdicts and computing the most severe one per group. Throws when a
 * required column is missing or a verdict cell holds unknown text.
 */
export const groupVerdicts = (table: {
  columns: readonly ColumnSchema[];
  rows: readonly unknown[][];
}): CheckGroup[] => {
  const idx = REQUIRED.map((name) => {
    const i = columnIndex(table.columns, name);
    if (i < 0)
      throw new Error(`bof-panes: verdict table is missing column "${name}"`);
    return i;
  });
  const [verdictIdx, checkIdIdx, titleIdx, citationIdx] = idx;

  interface Acc {
    checkId: string;
    checkTitle: string;
    citation: string;
    counts: VerdictCounts;
    rowIndices: number[];
  }
  const byId = new Map<string, Acc>();
  table.rows.forEach((row, rowIndex) => {
    const verdict = row[verdictIdx];
    if (!isVerdict(verdict))
      throw new Error(`bof-panes: unknown verdict "${String(verdict)}" in row ${rowIndex}`);
    const checkId = String(row[checkIdIdx]);
    let acc = byId.get(checkId);
    if (!acc) {
      acc = {
        checkId,
        checkTitle: String(row[titleIdx]),
        citation: String(row[citationIdx]),
        counts: { Pass: 0, Fail: 0, NeedsReview: 0, InfoNotAvailable: 0 },
        rowIndices: [],
      };
      byId.set(checkId, acc);
    }
    acc.counts[verdict]++;
    acc.rowIndices.push(rowIndex);
  });

  return [...byId.values()].map((acc) => ({
    ...acc,
    worst: VERDICTS.filter((v) => acc.counts[v] > 0).reduce((a, b) =>
      severityRank(b) > severityRank(a) ? b : a,
    ),
  }));
};
