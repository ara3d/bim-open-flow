# BimOpenFlow.Reports

The report generator: `ReportGenerator.FromRun(run, new ReportOptions(title))`
renders a run record as a static, archivable HTML document with no JavaScript
— provenance header (graph hash, engine version, timestamp, input content
hashes), a verdict summary when any recorded table follows the
Nodes.Compliance verdict-table convention (counts by verdict, severity
colors), capped evidence tables ("showing N of M"), and an appendix of all
node output hashes. Printable via simple @media print rules.

Deterministic for a given (run, options). A report never requires a running
host to read.

Depends on `BimOpenFlow.Publishing` and `Ara3D.DataFlowEngine.Runs`.
