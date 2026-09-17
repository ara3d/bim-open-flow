# BimOpenFlow.Nodes.Compliance

Verdict nodes for the BimOpenFlow dataflow engine: rule checks, required-data
checks, rollups, and unions. This is the evidence-bearing vocabulary — every
verdict carries its check id and citation, and data absence is reported, never
skipped. Kept BIM-free and small so the surface stays auditable.

Depends only on `Ara3D.DataFlowEngine.Abstractions` and
`Ara3D.DataFlowEngine.Expressions` (tables via `Ara3D.DataTable`, transitively).

## The verdict enum

`Verdict { Pass, Fail, NeedsReview, InfoNotAvailable }` — a local mirror of the
`Verdict` enum in `contracts/contracts.json` (this pack takes no contracts
dependency; member names must stay identical). Meanings:

| Verdict | Meaning |
|---|---|
| `Pass` | The rule expression held for the row |
| `Fail` | The rule expression did not hold (or required data was null) |
| `NeedsReview` | Failed, but flagged for human review by `reviewExpr` |
| `InfoNotAvailable` | A fact the check needs is absent (null expression result, or a required column missing entirely) |

Rollup severity order: `Fail` > `NeedsReview` > `InfoNotAvailable` > `Pass`.

## The verdict-table convention (the contract)

A **verdict table** is an `Ara3D.DataTable.IDataTable` that contains at least
these four columns, matched by exact case-sensitive name (`VerdictSchema` holds
the constants):

| Column | Type | Content |
|---|---|---|
| `verdict` | Text | Exactly one of `"Pass"`, `"Fail"`, `"NeedsReview"`, `"InfoNotAvailable"` |
| `checkId` | Text | Stable identifier of the check (e.g. `NBC-9.5.3.1`) |
| `checkTitle` | Text | Human-readable check title |
| `citation` | Text | The code/spec provision the check enforces |

Additional columns are allowed and expected: `check.rule` and `check.required`
emit the input table's columns (order preserved) followed by the four
convention columns, appended in exactly the order `verdict`, `checkId`,
`checkTitle`, `citation`. Their output table's name is the `checkId`. All four
metadata cells are non-null on every row.

The check nodes take **raw rows**, not verdict tables: if the input already
contains any of the four reserved column names they throw. Combine the outputs
of separate checks with `check.union`.

### Column types seen by expressions

Expressions address input columns by name. A column participates in the
expression environment when its CLR type maps to a scalar:

- `bool` → Boolean
- `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long` → Integer
- `float`, `double`, `decimal` → Number
- `string` → Text
- `Nullable<T>` maps as `T`; any other type is not addressable (referencing it
  is a type error)

A null cell evaluates as the null scalar and propagates through operators, so
missing data surfaces as a null expression result — never a crash and never a
silent skip.

## Nodes

All nodes: kind prefix `check.`, version 1, Pure. Exposed as
`ComplianceNodes.All` for registry composition.

### `check.rule` — in: Table → out: Table

Params: `checkId`, `title`, `citation` (Text); `expr` (Expression, Boolean over
the input columns); `reviewExpr` (Expression, optional — empty means unused).

Per row: `expr` true → `Pass`; false → `Fail`, unless `reviewExpr` evaluates
true for that row, then `NeedsReview`; null result (missing data) →
`InfoNotAvailable`. A null or false `reviewExpr` leaves the row `Fail`.

### `check.required` — in: Table → out: Table

Params: `checkId`, `title`, `citation`; `columns` (Text, comma-separated column
names that must be present and non-null per row).

If any listed column is missing from the table entirely, **every** row gets
`InfoNotAvailable` (and a warning names the missing columns). Otherwise a row
with a null cell in any listed column gets `Fail`; rows with all cells present
get `Pass`.

### `check.rollup` — in: verdict Table → out: summary Table

No params. Groups by `checkId` (rows in first-appearance order; `checkTitle`
and `citation` taken from the group's first row). Output columns, in order:

`checkId`, `checkTitle`, `citation` (Text), `passCount`, `failCount`,
`needsReviewCount`, `infoNotAvailableCount` (Integer), `worst` (Text) — the
most severe verdict present, by the severity order above.

### `check.union` — a: Table, b: Table → out: Table

No params. Both inputs must be verdict tables with identical column-name
sequences; the output is a's rows followed by b's rows (name and column
descriptors from a). Chain unions to combine more than two tables — `NodeSpec`
has fixed input lists, so variadic inputs are not expressible.

## Errors

Configuration mistakes throw `ArgumentException` from `Eval`: unparseable or
non-Boolean expressions, references to unknown columns, an empty `columns`
param, reserved-column collisions, non-verdict-table inputs to rollup/union,
mismatched union columns, unknown verdict text, or a null metadata cell.
Deterministic runtime evaluation errors (`EvaluationException`, e.g. modulo by
zero) propagate. Missing *data* never throws — it becomes `InfoNotAvailable`
or `Fail` per the node semantics above.
