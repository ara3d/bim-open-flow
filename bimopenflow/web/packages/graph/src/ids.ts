// Fresh node ids. Here, not in the app, because the editor creates nodes too
// (a dropped file, a promoted path: TKT-138); the app's ids.ts re-exports them.

/** The first `make(n)`, n = 1, 2, ..., that is not in `existing`. */
export const firstFree = (make: (n: number) => string, existing: Iterable<string>): string => {
  const taken = new Set(existing);
  for (let n = 1; ; n++) {
    const id = make(n);
    if (!taken.has(id)) return id;
  }
};

/**
 * A fresh node id derived from the kind's last dotted segment: "source.model"
 * -> "model1", "model2", ... skipping ids already in use. Node ids must not
 * contain dots (the port-ref separator).
 */
export function freshNodeId(kind: string, existing: Iterable<string>): string {
  const base = kind.split(".").pop() || "node";
  return firstFree((n) => `${base}${n}`, existing);
}
