# BimOpenFlow.Evidence

The compliance hand-off package: `EvidencePackage.Build` writes one .zip
holding the canonical graph (`graph.dfg.json`), the run record
(`run.run.json`), the rendered report (`report.html`), pinned input snapshots
under `inputs/`, and a canonical `manifest.json` (package format version
0.1.0, graph hash, run file name, SHA-256 of every member, caller-supplied
creation timestamp). Build validates that the graph's hash matches the run's.

`EvidencePackage.Verify` re-hashes every member against the manifest and
reports mismatches, missing members, and unlisted extras.

Signing is a TODO (attestation over the canonical manifest bytes).

Depends on `Ara3D.DataFlowEngine.Runs`, `Ara3D.NodeGraph`, and
`BimOpenFlow.Reports`.
