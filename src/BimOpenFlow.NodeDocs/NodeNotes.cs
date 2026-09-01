namespace BimOpenFlow.NodeDocs;

/// <summary>Hand-written behavioral notes per node kind, sourced from the node
/// implementations. Lives here (not in the output file) so docs/nodes.md stays
/// fully regenerable.</summary>
public static class NodeNotes
{
    public static string? For(string kind)
        => Notes.TryGetValue(kind, out var note) ? note : null;

    private static readonly IReadOnlyDictionary<string, string> Notes = new Dictionary<string, string>
    {
        ["bos.load"] =
            "Loads the .bos file into an in-memory DuckDB once and outputs three materialized "
            + "text views: `entities`, `parameters`, and `relations`, each deterministically ordered. "
            + "Results are cached per (file content hash, harmonize flag), so re-evaluations of "
            + "unchanged content never reload and edits to the file are picked up automatically. "
            + "With `harmonize` true, the data is passed through the BOS harmonizer (appends SI "
            + "canonical columns) before the views are built. A missing file is an error.",

        ["bos.query"] =
            "The input table is loaded into an in-memory DuckDB as table `t`. The query must be "
            + "read-only. The node predates `sql.query`, which generalizes it to four inputs.",

        ["table.filter"] =
            "The expression must be statically Boolean; a non-Boolean expression is an error. "
            + "A row is kept only when the expression is true — a null result excludes the row "
            + "(SQL WHERE semantics).",

        ["table.derive"] =
            "The new column's type comes from the expression's static type; rows where the "
            + "expression is null get a null cell. It is an error if the column name already "
            + "exists, or if the expression is always null (no type can be inferred).",

        ["table.aggregate"] =
            "Runs via DuckDB. Each aggregate is written `func(column) as name` with funcs "
            + "count, sum, min, max, avg; only count accepts `*`. Sums are cast (BIGINT for "
            + "integer columns, DOUBLE otherwise) so the result type is predictable. `groupBy` "
            + "may be empty (one summary row); when present, output rows are ordered by the "
            + "group columns for determinism.",

        ["table.sort"] =
            "Runs via DuckDB. Each comma-separated term is a column name with an optional "
            + "` desc` (or explicit ` asc`) suffix. Column names containing commas or spaces "
            + "cannot currently be expressed.",

        ["view3d.instances"] =
            "One row per placed mesh, with entity ids and world bounds. The loaded geometry "
            + "is cached by file content hash.",

        ["view3d.color"] =
            "Numeric value columns map through a gradient normalized over the column's "
            + "min..max range; text values map categorically, with palette indices assigned by "
            + "sorted distinct value so colors are stable under row reordering. A non-numeric "
            + "value column with a gradient colorMap warns and falls back to category10. "
            + "Instance rows with no match in the value table get gray; alpha is always 1.",

        ["view3d.isolate"] =
            "The ids table is matched on its column with the same name as `joinColumn`, or its "
            + "first column when no such column exists.",

        ["check.rule"] =
            "Per row: `expr` true is Pass; false is Fail, unless `reviewExpr` is also true, "
            + "which makes it NeedsReview; a null result (missing data) is InfoNotAvailable. "
            + "An empty `reviewExpr` means false never escalates to NeedsReview. The output is "
            + "the input columns plus the verdict columns (`verdict`, `checkId`, `checkTitle`, "
            + "`citation`).",

        ["check.required"] =
            "If any listed column is missing from the table, the node warns and every row is "
            + "InfoNotAvailable. Otherwise a row with a null cell in any listed column is Fail, "
            + "else Pass. Data absence is reported, never skipped.",

        ["check.rollup"] =
            "Input must be a verdict table. Output has one row per checkId in first-appearance "
            + "order, with per-verdict counts (`passCount`, `failCount`, `needsReviewCount`, "
            + "`infoNotAvailableCount`) and `worst`, the worst verdict present by severity "
            + "Fail > NeedsReview > InfoNotAvailable > Pass.",

        ["check.union"] =
            "Both inputs must be verdict tables with identical column-name sequences; the "
            + "output is a's rows followed by b's. The spec cannot express variadic inputs, so "
            + "the node takes exactly two — chain unions to combine more tables.",

        ["sink.exportCsv"] =
            "Effect: runs only inside a Run. Writes the table as RFC-4180 CSV with invariant "
            + "formatting. `delimiter` swaps the comma for any text (cells containing it are "
            + "quoted accordingly) and `header` false drops the header row. The file is "
            + "replaced atomically.",

        ["sink.exportParquet"] =
            "Effect: runs only inside a Run. Writes the table as a Parquet file, the hand-off "
            + "format for Power BI and data pipelines. `compression` picks zstd (default, "
            + "smallest), snappy (fastest to read), or none. Replaced atomically.",

        ["sink.exportJson"] =
            "Effect: runs only inside a Run. Writes the table as JSON: `records` is one array "
            + "of objects (set `indent` for human-readable output), `lines` is "
            + "newline-delimited JSON, one object per line, for streaming pipelines.",

        ["sink.exportXlsx"] =
            "Effect: runs only inside a Run. Writes the table to one sheet of an Excel "
            + "workbook. `replaceFile` produces a fresh single-sheet file; `replaceSheet` "
            + "refreshes only the named sheet of an existing workbook, so people keep their "
            + "own tabs while the graph owns this one. Header can be bold and columns "
            + "auto-fit.",

        ["sink.exportSqlite"] =
            "Effect: runs only inside a Run. Writes the table into a SQLite database in one "
            + "transaction: `replace` drops and recreates it, `append` adds rows to a table "
            + "with the same columns, `failIfExists` refuses to touch an existing one. "
            + "Booleans and integers become INTEGER, numbers REAL, everything else TEXT; "
            + "other tables in the database are untouched.",

        ["sink.exportDuckDb"] =
            "Effect: runs only inside a Run. Writes the table into a DuckDB database file — "
            + "the only node that ever opens one writable — with the same "
            + "replace/append/failIfExists modes, transactionally. Build a curated database "
            + "here, query it back with duck.query or duck.table.",

        ["sink.writePsets"] =
            "Effect: runs only inside a Run. Input rows (`entityId`, `psetName`, `paramName`, "
            + "`paramValue`) are grouped by (entityId, psetName) in first-appearance order; each "
            + "group becomes one IfcPropertySet attached to the entity, appended to a byte-exact "
            + "copy of the source file. An entity id not present in the source file is an error. "
            + "v1 limitation: every value is written as IFCTEXT; typed measures come later.",

        ["sink.report"] =
            "Effect: runs only inside a Run. The report is a minimal standalone HTML page: "
            + "the title followed by the table.",

        ["duck.read"] =
            "With `format` auto, the reader is inferred from the file extension (.csv, "
            + ".parquet, .json); any other extension is an error telling you to set `format`. "
            + "The loaded table is cached by (file content hash, reader), so unchanged files "
            + "never reload. A missing file is an error.",

        ["duck.query"] =
            "The database file is opened read-only (the node can never mutate it), and the SQL "
            + "is validated as a single SELECT or WITH statement before it runs.",

        ["sql.query"] =
            "Connected inputs load into an in-memory DuckDB as `t1`..`t4`, and `t` is a view "
            + "of `t1`, so single-table queries can just say `FROM t`. Ports `t2`..`t4` are "
            + "optional: unconnected ones are simply absent from the database. The SQL is "
            + "validated as a single read-only SELECT or WITH statement. The dialect is DuckDB's.",

        ["xlsx.read"] =
            "An empty `sheet` means the first worksheet; a named sheet that does not exist is "
            + "an error. Row 1 of the used range is the header (blank headers become Column1, "
            + "Column2, ...). Each column's type is inferred: if all non-null cells share one "
            + "CLR type that type wins, otherwise the column is text. Dates are read as "
            + "ISO-8601 text. `headerRow` (default 1) names the header's row within the region "
            + "being read — rows above it are skipped, the junk-rows-above-the-header fix — "
            + "and `range` (empty = used range) restricts reading to an A1-style rectangle "
            + "like B3:F100; the two compose. The result is cached by content hash and "
            + "parameters.",

        ["sqlite.query"] =
            "The database file is opened read-only, and the SQL is validated as a single "
            + "SELECT or WITH statement. SQLite columns are dynamically typed per row, so each "
            + "result column is unified: one non-null CLR type wins, a mix of integer and real "
            + "widens to real, anything else lands as text.",

        ["table.join"] =
            "Joins b's columns onto a by key, matching on canonical cell text; `bKey` defaults "
            + "to `aKey` when blank. Mode `left` keeps every a row (unmatched rows get null b "
            + "cells); `inner` keeps only matches; `full` keeps left's rows plus unmatched b "
            + "rows (with b's key surfaced in the key column); `semi` keeps a rows that have "
            + "a match without attaching b columns; `anti` keeps a rows with no match. "
            + "Unmatched rows and duplicate keys in b are warned about in every mode, never "
            + "silent — with duplicates, the first b occurrence wins. b's key column is "
            + "dropped from the output, and a b column whose name collides with an a column "
            + "(case-insensitive) is suffixed `_b`.",

        ["table.setOp"] =
            "Row-set algebra on a key column; a's columns and row order pass through. "
            + "`intersect` keeps a rows whose key appears in b, `subtract` keeps those whose "
            + "key does not, and `union` appends b rows whose key is absent from a. Union "
            + "requires b to have exactly a's column set (matched case-insensitively); any "
            + "missing or extra column is an error.",

        ["table.project"] =
            "Keeps the named columns in the given order. A name with no matching column warns "
            + "and is skipped rather than erroring; naming no columns at all is an error.",

        // ── DuckDB readers ──────────────────────────────────────────────────

        ["csv.read"] =
            "Reads one CSV file or a glob of files via DuckDB `read_csv`, with typed delimiter, "
            + "quote, header, skip-rows, null-text, encoding, and type-inference options. A glob "
            + "unions every matching file and appends a `filename` column so provenance survives; "
            + "a glob matching nothing, like a missing file, is an error. With `header` false, "
            + "columns are named Column1..N. Results are cached by file content hash plus "
            + "parameter values.",

        ["parquet.read"] =
            "Reads a Parquet file or glob of files via DuckDB `read_parquet`. Parquet is "
            + "self-describing, so path is the only parameter. Content-hash cached like csv.read.",

        ["json.read"] =
            "Reads a JSON file via DuckDB `read_json`; `layout` selects the file shape (auto, "
            + "records = one array of objects, lines = newline-delimited). With `flatten` true, "
            + "one level of nested objects expands into dotted columns; deeper nesting stays a "
            + "struct column for `sql.query` to unpack.",

        ["duck.table"] =
            "Reads one named table from a .duckdb database opened read-only, so the node can "
            + "never mutate the file. An unknown table name is an error naming the table; the "
            + "no-SQL companion to duck.query.",

        ["duck.tables"] =
            "Lists the tables of a .duckdb database (read-only) as a table with `name`, "
            + "`columnCount`, and real `rowCount` per table, ordered by name. The discovery step "
            + "before duck.table or duck.query.",

        // ── XLSX, SQLite, and generators ────────────────────────────────────

        ["xlsx.sheets"] =
            "Lists a workbook's worksheets as a table: `name`, `index` (1-based position), and "
            + "the used range's `rowCount`/`columnCount` (0 for empty sheets). Discovery before "
            + "naming a sheet in xlsx.read; cached by file content hash.",

        ["sqlite.table"] =
            "Reads one whole table (`SELECT *`, read-only, case-insensitive name match) with the "
            + "pack's column-type unification rules; an unknown table is an error naming it. The "
            + "no-SQL companion to sqlite.query.",

        ["sqlite.tables"] =
            "Lists a database's user tables (`name`, `columnCount`, `rowCount`) in name order, "
            + "read-only, with sqlite_ internals and views excluded.",

        ["table.inline"] =
            "Builds a small table from a JSON array of objects typed into the node. Column types "
            + "are inferred (bool/integer/number/text); a column mixing types is an error naming "
            + "the column; nulls and missing keys land as nulls; `[]` gives an empty table.",

        ["table.range"] =
            "One numeric column from `start` to `stop` by `step`, inclusive of stop when a step "
            + "lands exactly on it; negative steps count down; step 0 is an error.",

        ["table.calendar"] =
            "One ISO-8601 date column from `start` to `end` inclusive, stepping by "
            + "day/week/month/quarter/year with real calendar arithmetic. The calendar spine for "
            + "gap-filling time series via table.join.",

        // ── TableOps: rows, columns, reshape, window ────────────────────────

        ["table.limit"] =
            "Keeps `count` rows starting at `offset` in the table's deterministic order — "
            + "top-N after table.sort, paging through inspection.",

        ["table.distinct"] =
            "Removes duplicate rows. With no columns named, whole rows are compared; with key "
            + "columns named, the first row per key (in input order) is kept with all its "
            + "columns. Output preserves first-occurrence order.",

        ["table.sample"] =
            "Takes a seeded random sample: `rows` mode keeps a fixed number of rows "
            + "(reservoir), `fraction` mode keeps each row with the given probability "
            + "(bernoulli). The same seed returns the same sample on the same machine "
            + "(DuckDB documents REPEATABLE as reproducible for a fixed thread count), and "
            + "sampled rows keep their input order.",

        ["table.concat"] =
            "Appends b's rows after a's. Strict mode requires both tables to have identical "
            + "column sequences and errors naming the difference; byName matches columns by "
            + "name and fills columns missing from one side with nulls.",

        ["table.rename"] =
            "Renames columns via comma-separated `old=new` pairs. Unknown old names warn and "
            + "are skipped; a new name that would collide with a remaining column is an error.",

        ["table.drop"] =
            "Removes the named columns and keeps everything else — the complement of "
            + "table.project. Unknown names warn; dropping every column is an error.",

        ["table.cast"] =
            "Converts one column to boolean, integer, number, text, date, or datetime, in "
            + "place or as a new named column. onError `null` turns unconvertible values into "
            + "nulls and warns with the count; date/datetime accept ISO-8601 text only and "
            + "come back as ISO text.",

        ["table.splitColumn"] =
            "Splits a text column on a separator into new columns, one per requested name. "
            + "Rows with fewer parts than names get nulls; extra parts are dropped; `keep` "
            + "retains the original column.",

        ["table.pivot"] =
            "Turns long data wide: each distinct value of nameColumn becomes a column, filled "
            + "by aggregating valueColumn per groupBy key. New columns are ordered by sorted "
            + "value; `first` takes the first value in input order.",

        ["table.unpivot"] =
            "Turns wide data long: the chosen columns fold into name/value rows next to the "
            + "kept id columns. Mixed-type columns widen to text with a warning; null cells "
            + "produce no row.",

        ["table.transpose"] =
            "Rows become columns: the header column's values name the new columns and every "
            + "other column becomes a row. All values widen to text; limited to 1,000 rows — "
            + "meant for small summary tables.",

        ["table.window"] =
            "Adds one window-function column: rankings (rowNumber/rank/denseRank), lag/lead, "
            + "cumulative sum, moving average, or percent of total, optionally partitioned "
            + "and ordered. Input rows and their order are unchanged.",

        ["table.schema"] =
            "Outputs the table's shape as a table: one row per column with its name, wire "
            + "type (Boolean/Integer/Number/Text), and position — so schemas can be diffed, "
            + "checked, and documented like any other data.",

        ["table.profile"] =
            "Profiles every column via DuckDB SUMMARIZE, projected to a fixed column set: "
            + "type, row count, exact null count, approximate distinct count, min/max "
            + "(lexical for text), and mean (null for non-numeric).",

        // ── Cleaning ────────────────────────────────────────────────────────

        ["table.fillNulls"] =
            "Fills nulls in the listed columns with a typed constant, or with the nearest "
            + "earlier (forward) or later (backward) non-null value in the table's row order. "
            + "Forward fill is the merged-cells-out-of-Excel fix; set partitionBy to stop "
            + "values leaking across group boundaries. The constant is cast to each column's "
            + "type and an uncastable value is an error.",

        ["table.dropNulls"] =
            "Drops rows where any (or, with mode all, every one) of the listed columns is "
            + "null; leaving columns empty checks every column. Never silent: the dropped-row "
            + "count is reported as a warning.",

        ["table.dedupe"] =
            "Keeps one row per key combination: the first or last by orderBy (same syntax as "
            + "table.sort), or by input row order when orderBy is empty. Kept rows come out in "
            + "their original order, and the number of removed duplicates is reported as a "
            + "warning. Keep-latest-per-id is keep last with orderBy on a timestamp column.",

        ["table.replace"] =
            "Rewrites values in one text column by exact match (whole-value recode), "
            + "substring, or regular expression (with group references like \\1), optionally "
            + "case-insensitive. To recode a sentinel to null, replace it with the empty "
            + "string and follow with table.cast using onError null.",

        ["text.transform"] =
            "Applies trim, upper, lower, or normalizeSpace (trim plus collapse runs of "
            + "whitespace to one space) in place to the named text columns, or to every text "
            + "column when none are named. The standard pre-join step for keys that fail on "
            + "invisible whitespace.",

        ["text.extract"] =
            "Adds one new column holding a regex capture group (0 = the whole match) pulled "
            + "from a text column; rows that don't match get null. Pair with table.cast to "
            + "make the extract numeric, e.g. pulling \"03\" out of \"Level 03 - Zone B\".",

        // ── Dates ───────────────────────────────────────────────────────────

        ["date.parse"] =
            "Turns a text column into canonical ISO-8601 date text, using a strptime format "
            + "(e.g. `%d/%m/%Y`) or a plain ISO cast when the format is empty. `onError` picks "
            + "between rejecting the table on the first bad value and nulling bad values with a "
            + "warning that counts them. The entry point of the Dates set: every other date node "
            + "requires an ISO date column and points here when it finds anything else.",

        ["date.part"] =
            "Adds one Integer column holding a component of an ISO date column: year, quarter, "
            + "month, week, day of month, ISO day of week (Monday = 1), day of year, hour, "
            + "minute, or second.",

        ["date.truncate"] =
            "Rounds an ISO date column down to the start of its year, quarter, month, week "
            + "(Monday), day, or hour. The result is still a date, so it sorts and joins "
            + "correctly across years. Empty `name` replaces the column in place.",

        ["date.diff"] =
            "Adds an Integer column counting unit boundaries (years to seconds, default days) "
            + "from column `a` to column `b`, negative when `b` is earlier.",

        ["date.offset"] =
            "Shifts an ISO date column by a signed whole number of years, months, days, hours, "
            + "or minutes using calendar rules: Jan 31 plus one month lands on the end of "
            + "February. Empty `name` replaces the column in place.",

        ["date.filter"] =
            "Keeps rows whose ISO date column falls in the half-open range [from, to), so "
            + "adjacent ranges never overlap; an empty bound leaves that side open, and both "
            + "bounds empty passes the table through with a warning. Promoted to graph "
            + "parameters, the bounds become the report's date-range control.",
    };
}
