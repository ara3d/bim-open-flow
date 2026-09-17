# BimOpenFlow.Nodes.Effects

All Run-gated sinks in one place. Isolating effects keeps every other node pack
pure, so the purity rule is enforceable by project reference alone: only this
project may write files or modify models.

Every node here:

- has Capability `Effect` (the engine only evaluates it inside an explicit Run);
- additionally throws `InvalidOperationException` itself when
  `context.IsRun == false` (defense in depth);
- still has pure outputs: the summary table each sink returns is a
  deterministic function of its inputs and parameters.

## Nodes (`EffectNodes.All`)

| Kind | Inputs | Params | Output (one-row summary) |
|---|---|---|---|
| `sink.exportCsv` v1 | in: Table | path (FilePath) | path, rowCount |
| `sink.writePsets` v1 | in: Table with entityId (Integer, STEP express id), psetName, paramName, paramValue (Text) | sourcePath, targetPath (FilePath) | targetPath, entitiesTouched, valuesWritten |
| `sink.report` v1 | in: Table | path (FilePath), title (Text) | path, rowCount |

`sink.exportCsv` writes RFC-4180 CSV: CRLF rows, header row, invariant
formatting (null = empty, booleans lowercase), quotes only when a cell contains
a quote, comma, or line break.

`sink.writePsets` uses `Ara3D.Ifc.Editing` for byte-exact write-back: the file
at `sourcePath` is copied to `targetPath` with new
IfcPropertySingleValue/IfcPropertySet/IfcRelDefinesByProperties lines appended
before ENDSEC; untouched bytes are identical. Rows are grouped by
(entityId, psetName) in first-appearance order. v1 limitation: every value is
written as IFCTEXT; typed measures and units are deferred until the editing
API grows a typed value path.

`sink.report` writes a minimal standalone HTML file (title + table).

## Deferred: GLB and BOS exports

The charter lists GLB and BOS export sinks, and `docs/bimopenflow-structure.md`
names Ara3D.IO.GltfExporter as a dependency, but this project is not granted
the geometry/schema dependencies (Ara3D.Ifc.Mesher / Ara3D.BimOpenSchema.IO)
those sinks need. This is a structure-doc inconsistency to resolve; until then:

<!-- TODO: add sink.exportGlb and sink.exportBos once the structure doc grants the geometry/schema dependencies. -->

## Dependencies

Ara3D.DataFlowEngine.Abstractions, Ara3D.DataFlowEngine.Runs,
Ara3D.Ifc.Editing (forces net8.0-windows/x64); packages Ara3D.DataTable,
Ara3D.Utils.
