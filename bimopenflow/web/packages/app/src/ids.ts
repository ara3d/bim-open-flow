const firstFree = (make: (n: number) => string, existing: Iterable<string>): string => {
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

/** The next free "untitled-N" analysis id. */
export function freshUntitledId(existing: Iterable<string>): string {
  return firstFree((n) => `untitled-${n}`, existing);
}

/** The id for a copy of `source`: "door-schedule-copy", then "-copy-2", ...
 *  so a flow started from a template keeps the template's name in view. */
export function freshCopyId(source: string, existing: Iterable<string>): string {
  return firstFree((n) => (n === 1 ? `${source}-copy` : `${source}-copy-${n}`), existing);
}
