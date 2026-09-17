# BimOpenFlow.Host.Catalog

Model discovery and the IFC to BOS conversion pipeline for the BimOpenFlow
host. Owns all knowledge of where model files live and how they become BOS.

## What it does

- `ModelCatalog` is constructed over one or more root directories plus a cache
  directory. `Scan()` walks the roots and returns an immutable
  `IReadOnlyList<ModelEntry>` for every `.ifc` and `.bos` file found.
- `GetBos(entry)` returns the path to the BOS form of a model. For `.bos`
  sources that is the file itself. For `.ifc` sources the file is converted
  (via `Ara3D.BimOpenSchema.IO.IfcToBosConverter`) into the cache directory,
  named by the source file's SHA-256 content hash, so a model is only
  re-converted when its content changes.
- `GetInfo(entry)` reads the BOS archive and reports entity, parameter,
  document, and relation counts.

## Decisions

- **Id**: slug of the root-relative path (lowercased, separators and spaces
  become `-`). Stable across restarts and across content edits. A collision
  (same relative path under two roots) gets an 8-character content-hash suffix.
- **ContentHash**: computed eagerly during `Scan()` (full lowercase SHA-256).
  It is needed for cache keying anyway; memoizing by size and mtime is a
  tracked TODO if scans get slow.
- **Concurrency**: conversion writes to a uniquely named temp file in the
  cache directory, then renames onto `{hash}.bos`. The first rename wins;
  losers discard their temp file. No lock files.
- **Re-scan**: `Scan()` is pure discovery each call. No file watchers in v1;
  the host polls.

## Testing

The converter is a constructor-injected `IIfcConverter`, so tests exercise
discovery and cache logic with a stub and no real IFC files. One real
conversion test runs against `data/duplex.ifc` when present
(category `RequiresData`).
