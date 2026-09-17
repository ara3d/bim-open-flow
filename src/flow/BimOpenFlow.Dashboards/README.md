# BimOpenFlow.Dashboards

The dashboard generator: `DashboardGenerator.FromRun(run, spec, vizBundle)`
turns a frozen run record into one self-contained interactive HTML file — the
viz bundle inline, the referenced recorded tables embedded as `TableData`
JSON, and an init script mounting a `BofViz` component (DataTableView,
BarChart, or LineChart) per `DashboardSpec` item.

Deterministic for a given (run, spec, bundle). The live variant that observes
a running host is a TODO (needs the host SSE channel).

Depends on `BimOpenFlow.Publishing` and `Ara3D.DataFlowEngine.Runs`.
