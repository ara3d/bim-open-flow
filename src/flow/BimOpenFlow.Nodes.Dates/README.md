# BimOpenFlow.Nodes.Dates

Parse, extract, truncate, shift, difference, and range-filter over ISO-8601 date
columns, backed by DuckDB date functions. Exposes `DatesNodes.All` for registry
composition. All nodes are version 1 and Pure.

Dates travel as text in the canonical wire form: `yyyy-MM-dd` at midnight,
`yyyy-MM-ddTHH:mm:ss` otherwise. Every node except `date.parse` requires its input
column to already hold that form and points at `date.parse` when it does not.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `date.parse` | table | table | column (Text), format (Text, strptime pattern; empty = ISO-8601 cast), onError (Enum error/null, default error), name (Text, empty = in place) |
| `date.part` | table | table | column (Text), part (Enum year/quarter/month/week/dayOfMonth/dayOfWeek/dayOfYear/hour/minute/second), name (Text) |
| `date.truncate` | table | table | column (Text), period (Enum year/quarter/month/week/day/hour), name (Text, empty = in place) |
| `date.offset` | table | table | column (Text), amount (Integer, signed), unit (Enum years/months/days/hours/minutes, default days), name (Text, empty = in place) |
| `date.diff` | table | table | a (Text), b (Text), unit (Enum years/months/days/hours/minutes/seconds, default days), name (Text) |
| `date.filter` | table | table | column (Text), from (DateTime, inclusive), to (DateTime, exclusive) |

## Semantics

- Row order is preserved: `DateSql` materializes an ordinal column in C# and
  orders the projection by it, never by `row_number()` over an unordered scan.
- `date.part` returns integers; `dayOfWeek` is ISO (Monday = 1).
- `date.diff` counts unit boundaries crossed from `a` to `b` (DuckDB
  `date_diff`), so it is negative when `b` is earlier.
- `date.offset` uses interval arithmetic, so adding one month to 2024-01-31
  gives 2024-02-29.
- `date.filter` keeps rows with `from <= column < to` and drops null dates; an
  empty bound is open, and both empty warns and passes the table through.
- The in-place-unless-named convention: an empty `name` replaces `column`; a
  non-empty `name` appends a new column and errors if it already exists.

## Errors

Unknown columns, non-ISO input, an unparsable value under `onError=error`, and
bad parameters throw `ArgumentException` with the node kind prefixed; DuckDB
failures are rethrown with the same prefix.
