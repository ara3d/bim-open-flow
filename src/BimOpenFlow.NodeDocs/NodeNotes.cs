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
            "Effect: runs only inside a Run. Writes the table as RFC-4180 CSV with a header "
            + "row and invariant formatting.",

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
            + "ISO-8601 text. The result is cached by (file content hash, sheet).",

        ["sqlite.query"] =
            "The database file is opened read-only, and the SQL is validated as a single "
            + "SELECT or WITH statement. SQLite columns are dynamically typed per row, so each "
            + "result column is unified: one non-null CLR type wins, a mix of integer and real "
            + "widens to real, anything else lands as text.",

        ["table.join"] =
            "Joins b's columns onto a by key, matching on canonical cell text; `bKey` defaults "
            + "to `aKey` when blank. Mode `left` keeps every a row (unmatched rows get null b "
            + "cells); `inner` keeps only matches. Unmatched rows and duplicate keys in b are "
            + "warned about, never silent — with duplicates, the first b occurrence wins. b's "
            + "key column is dropped from the output, and a b column whose name collides with "
            + "an a column (case-insensitive) is suffixed `_b`.",

        ["table.setOp"] =
            "Row-set algebra on a key column; a's columns and row order pass through. "
            + "`intersect` keeps a rows whose key appears in b, `subtract` keeps those whose "
            + "key does not, and `union` appends b rows whose key is absent from a. Union "
            + "requires b to have exactly a's column set (matched case-insensitively); any "
            + "missing or extra column is an error.",

        ["table.project"] =
            "Keeps the named columns in the given order. A name with no matching column warns "
            + "and is skipped rather than erroring; naming no columns at all is an error.",
    };
}
