# BimOpenFlow.Publishing

The shared document-emission layer under the Dashboards and Reports
generators: self-contained HTML assembly (`HtmlDocumentBuilder`), the inline
CSS theme (`HtmlTheme`, `--bof-*` custom properties, light default), escaped
static tables (`HtmlTables`), IDataTable → contracts `TableData` JSON
(`TableJson`, the shape the viz components consume), the built viz bundle
loader (`VizBundle`), and data-URI asset embedding (`DataUri`).

Everything here is deterministic: the same inputs produce the same bytes —
no timestamps, no machine state. HTML escaping happens once, in `Html`.

Depends on `BimOpenFlow.Contracts` and the Ara3D SDK packages only.
