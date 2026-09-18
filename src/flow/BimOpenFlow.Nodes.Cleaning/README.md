# BimOpenFlow.Nodes.Cleaning

Nulls, duplicates, text noise, and value replacement over one flowing table, each
a typed facade over one generated DuckDB clause. Exposes `CleaningNodes.All` for
registry composition. All nodes are version 1 and Pure.

## Nodes

| Kind | Inputs | Outputs | Params |
|---|---|---|---|
| `table.dropNulls` | table | table | columns (Text, comma-separated; empty = all columns), mode (Enum any/all, default any) |
| `table.fillNulls` | table | table | columns (Text), strategy (Enum constant/forward/backward, default constant), value (Text, typed to the column), partitionBy (Text, optional) |
| `table.dedupe` | table | table | keys (Text, comma-separated), keep (Enum first/last, default first), orderBy (Text; empty = input row order) |
| `table.replace` | table | table | column (Text), find (Text), replaceWith (Text), match (Enum exact/substring/regex, default exact), caseSensitive (Boolean, default true) |
| `text.transform` | table | table | columns (Text; empty = every text column), op (Enum trim/upper/lower/normalizeSpace, default trim) |
| `text.extract` | table | table | column (Text), pattern (Text, regex), group (Integer, default 1; 0 = whole match), name (Text, new column) |

## Semantics

- Row order is preserved through a hidden ordinal column
  (`CleaningTables`), so `forward`/`backward` fills and `first`/`last` dedupe
  follow the input order when `orderBy` is empty.
- `table.dropNulls` and `table.dedupe` warn with the number of rows removed.
- `table.fillNulls` with `constant` parses `value` to the column's type;
  `forward`/`backward` take the nearest earlier or later non-null value, within
  `partitionBy` groups when given.
- `table.replace` applies to text columns only; `regex` uses DuckDB's
  `regexp_replace`, and `caseSensitive=false` adds the `i` flag.
- `text.extract` yields null where the pattern does not match.
- `text.transform` edits the named columns in place; `normalizeSpace` trims and
  collapses internal whitespace runs to one space.

## Errors

Unknown columns, a non-text column for `table.replace` or `text.extract`, an
unparsable `value`, and an invalid regex throw `ArgumentException` with the node
kind prefixed to the message.
