// Flow templates: the sample graphs the host seeds, described for the start
// page. The catalog itself is templates.generated.ts, written by
// scripts/build-flow-templates.mjs; this file holds its types and the pure
// grouping the start page renders. DOM-free.

/** One sample graph the host can seed, as the start page shows it. */
export interface FlowTemplate {
  /** The analysis id the host seeds (the sample file's stem, or a workflows.json id). */
  readonly id: string;
  /** The sample folder it comes from: "bim-analyses", "nrc-analyses", ... */
  readonly folder: string;
  /** From workflows.json's title, else the id. */
  readonly title: string;
  /** One line from the folder README; "" when the README has none. */
  readonly description: string;
  readonly nodeCount: number;
  /** Distinct node kinds, sorted. */
  readonly kinds: readonly string[];
}

/** A template paired with whether the host currently lists its id. */
export interface PresentTemplate extends FlowTemplate {
  readonly present: boolean;
}

export interface TemplateGroup {
  readonly folder: string;
  readonly label: string;
  readonly templates: readonly PresentTemplate[];
}

/** Known folders in display order, with their on-screen labels. */
const FOLDER_LABELS: readonly (readonly [string, string])[] = [
  ["analyses", "Tables"],
  ["bim-analyses", "BIM"],
  ["nrc-analyses", "NRC paper"],
  ["showcase-analyses", "Showcase"],
  ["view3d-analyses", "3D"],
  ["snowdon-analyses", "Snowdon"],
  ["duckdb-analyses", "DuckDB studio"],
];

const folderRank = (folder: string): number => {
  const i = FOLDER_LABELS.findIndex(([f]) => f === folder);
  return i < 0 ? FOLDER_LABELS.length : i;
};

export const folderLabel = (folder: string): string =>
  FOLDER_LABELS.find(([f]) => f === folder)?.[1] ?? folder;

/** Groups templates by folder, known folders first in a fixed order and
 *  unknown ones after by name, keeping each folder's template order, and
 *  marks which ids are present in the host's analysis list. */
export function groupTemplates(
  templates: readonly FlowTemplate[],
  present: ReadonlySet<string>,
): TemplateGroup[] {
  const byFolder = new Map<string, PresentTemplate[]>();
  for (const t of templates) {
    const list = byFolder.get(t.folder) ?? [];
    list.push({ ...t, present: present.has(t.id) });
    byFolder.set(t.folder, list);
  }
  return [...byFolder.keys()]
    .sort((a, b) => folderRank(a) - folderRank(b) || (a < b ? -1 : a > b ? 1 : 0))
    .map((folder) => ({ folder, label: folderLabel(folder), templates: byFolder.get(folder)! }));
}
