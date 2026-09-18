// The numeric entity keys graph instance tables use, derived from a loaded
// model's object records. Loaders set `ref.objectId` to the model's own row
// id ("bos:<entity row>") and `sourceId` to the authoring id (the STEP express
// id for BOS files converted from IFC), and the geometry pack keys its tables
// by that source id.
import type { ObjectRecord } from "@bim-open-toolkit/model";

/** An object's entity key: its numeric source id when present, else the `bos:<row>` id. NaN for no record. */
export const entityKeyOf = (record: ObjectRecord | undefined): number => {
  if (!record) return NaN;
  const source = record.sourceId === undefined ? NaN : Number(record.sourceId);
  return Number.isFinite(source) ? source : Number(record.ref.objectId.replace(/^bos:/, ""));
};

/** Entity key per object id, for resolving pick keys (whose last `|` segment is the object id). */
export const entityKeysByObjectId = (objects: readonly ObjectRecord[]): ReadonlyMap<string, number> =>
  new Map(objects.map((record) => [record.ref.objectId, entityKeyOf(record)]));
