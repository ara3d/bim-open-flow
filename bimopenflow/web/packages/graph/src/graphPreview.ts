import type { GraphDocument } from "@bimopenflow/state";

export const nodeTitle = (kind: string): string => ({
  "view3d.scene": "Model",
  "view3d.categoryStyle": "Color by category",
  "view3d.section": "Section plane",
  "view3d.sectionBox": "Section box",
  "view3d.sectionRange": "Section band",
  "view3d.explode": "Explode categories",
  "view3d.projection": "Projection",
  "view3d.environment": "Environment",
  "view3d.tint": "Model color",
}[kind] ?? kind);

export function upstreamIds(document: GraphDocument, nodeId: string | null): Set<string> {
  const ids = new Set<string>(), pending = nodeId ? [nodeId] : [];
  while (pending.length) {
    const id = pending.pop()!;
    if (ids.has(id)) continue;
    ids.add(id);
    for (const edge of document.structure.edges)
      if (edge.to.split(".")[0] === id) pending.push(edge.from.split(".")[0]!);
  }
  return ids;
}

export function previewAfterEdit(document: GraphDocument, edited: string, preview: string | null): string {
  return preview && upstreamIds(document,preview).has(edited) ? preview : edited;
}
