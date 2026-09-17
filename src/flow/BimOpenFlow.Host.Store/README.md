# BimOpenFlow.Host.Store

Persistence for graph documents and run records: the analysis library on disk,
versioned saves, and run archival. Storage semantics only — no HTTP, no
evaluation.

## On-disk layout

One root directory holds the whole library. Everything is plain files in
canonical JSON, so the library is human-browsable and diffs cleanly in git.

```
<root>/
  <analysisId>/                     one folder per analysis
    current.dfg.json                the live document (canonical JSON)
    name.txt                        optional display name; falls back to the id
    versions/
      0001.dfg.json                 archived previous versions, oldest first
      0002.dfg.json                 (zero-padded sequence, at least 4 digits)
    runs/
      20260831T120000123Z-ab12cd34.run.json
                                    frozen run records:
                                    <file-safe timestamp>-<graphHash first 8>.run.json
  .trash/
    <analysisId>/                   deleted analyses, moved whole (reversible);
    <analysisId>-2/                 repeat deletes get a numeric suffix
```

- **Analysis ids** are lowercase slugs: `a-z`, `0-9`, interior hyphens. No
  dots, slashes, or uppercase — the id is the directory name.
- **Names**: the display name is the id itself unless a `name.txt` sidecar
  exists in the analysis folder (its trimmed content wins). The sidecar is
  optional and hand-editable.
- **Versioned saves**: every `Save` whose canonical bytes differ from
  `current.dfg.json` first copies the old current into `versions/` under the
  next sequence number, then atomically replaces current. Saving byte-identical
  content is a no-op (returns false, writes nothing, archives nothing).
- **Run timestamps** are the record's RFC 3339 UTC timestamp with the
  punctuation removed (colons are not legal in Windows file names):
  `2026-08-31T12:00:00.123Z` → `20260831T120000123Z`. Names sort
  chronologically.
- **Runs are immutable**: `SaveRun` refuses to overwrite an existing file
  (same timestamp + graph hash) with an `IOException`. Runs are frozen
  evidence; they are never rewritten.
- **Delete is reversible**: `Delete` moves the analysis folder into `.trash/`
  rather than removing it. `List` ignores `.trash` (and any dot-directory,
  since dots are not legal in ids). Restore by moving the folder back.

## Concurrency (v1)

Last-writer-wins. All writes go through a temp file in the target directory
followed by an atomic rename, so readers see complete files only — but two
concurrent savers of the same analysis may both archive the same previous
version, and the later rename wins. No locks are held across calls. This is
acceptable for a single-host library; optimistic concurrency (compare graph
hash before replace) can layer on later without changing the layout.

## API

`AnalysisStore(rootDir)` — creates the root directory if missing.

| Member | Behavior |
|---|---|
| `List()` | entries (id, name), sorted by id; only folders with a `current.dfg.json` |
| `Create(id)` | validates the id, saves an empty document; throws if it exists |
| `Load(id)` / `Save(id, doc)` | current document; Save returns false on a byte-identical no-op |
| `History(id)` | archived versions (sequence, graph hash, file name), oldest first |
| `LoadVersion(id, n)` | one archived version |
| `Delete(id)` | move to `.trash/` |
| `SaveRun(id, record)` | archive a run, returns the file name; refuses overwrite |
| `ListRuns(id)` | run file names in chronological order |
| `LoadRun(id, fileName)` | one archived run |

**Depends on:** Ara3D.NodeGraph, Ara3D.DataFlowEngine.Runs.
