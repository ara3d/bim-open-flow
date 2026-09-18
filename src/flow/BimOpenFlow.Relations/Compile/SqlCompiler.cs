namespace BimOpenFlow.Relations;

/// <summary>Plan to DuckDB SQL: one CTE per distinct plan node in post-order. Needs the
/// input schemas of joins and aggregates to name their output columns, so it infers
/// schemas as it goes and fails with the first schema error.</summary>
public static class SqlCompiler
{
    public static CompiledQuery Compile(this Plan plan, ICatalog catalog)
        => Compile(plan, new SchemaCache(catalog));

    public static CompiledQuery Compile(this Plan plan, SchemaCache schemas)
    {
        schemas.Infer(plan).Require();
        var order = plan.PostOrder();
        var names = order.Select((p, i) => (p, name: $"n{i + 1}")).ToDictionary(x => x.p, x => x.name);
        var ctes = order.Select(p => $"{names[p]} AS ({Body(p, p.Inputs.Select(i => names[i]).ToList(), schemas)})");
        var sources = order.OfType<ReadCsv>().Select(r => new SourceUse(r.Source, SourceKind.Csv, r.Path))
            .Concat(order.OfType<ReadTable>().Select(r => new SourceUse(r.Source, SourceKind.Table, r.Table)))
            .Distinct().ToList();
        return new($"WITH {string.Join(",\n     ", ctes)}\nSELECT * FROM {names[plan]}", sources);
    }

    private static string Body(Plan plan, IReadOnlyList<string> inputs, SchemaCache schemas)
        => plan switch
        {
            ReadCsv r => $"SELECT * FROM {new SourceUse(r.Source, SourceKind.Csv, r.Path).Ident}",
            ReadTable r => $"SELECT * FROM {new SourceUse(r.Source, SourceKind.Table, r.Table).Ident}",
            RawSql r => RawSqlBody(r, inputs),
            Select s => $"SELECT {Columns(s.Columns)} FROM {inputs[0]}",
            Rename r => $"SELECT {RenameList(r, Schema(r.Input, schemas))} FROM {inputs[0]}",
            Cast c => $"SELECT {CastList(c, Schema(c.Input, schemas))} FROM {inputs[0]}",
            Derive d => $"SELECT *, {d.Expression.ToSql()} AS {d.Name.Ident()} FROM {inputs[0]}",
            Filter f => $"SELECT * FROM {inputs[0]} WHERE {f.Predicate.ToSql()}",
            Distinct => $"SELECT DISTINCT * FROM {inputs[0]}",
            Sort s => $"SELECT * FROM {inputs[0]} ORDER BY {string.Join(", ", s.Keys.Select(k => k.Column.Ident() + (k.Descending ? " DESC" : " ASC")))}",
            Limit l => $"SELECT * FROM {inputs[0]} LIMIT {l.Count}" + (l.Offset > 0 ? $" OFFSET {l.Offset}" : ""),
            Join j => JoinBody(j, inputs, Schema(j.Left, schemas), Schema(j.Right, schemas)),
            Union => string.Join(" UNION ALL ", inputs.Select(i => $"SELECT * FROM {i}")),
            Aggregate a => AggregateBody(a, inputs[0], Schema(a.Input, schemas)),
            _ => throw new ArgumentException($"No SQL for {plan.GetType().Name}.", nameof(plan)),
        };

    private static Schema Schema(Plan plan, SchemaCache schemas)
        => schemas.Infer(plan).Require();

    private static string Columns(IEnumerable<string> names)
        => string.Join(", ", names.Select(Ident));

    private static string Ident(string name)
        => name.Ident();

    private static string RawSqlBody(RawSql r, IReadOnlyList<string> inputs)
    {
        if (inputs.Count == 0) return r.Sql;
        var bindings = inputs.Select((name, i) => $"t{i + 1} AS (SELECT * FROM {name})");
        return $"WITH {string.Join(", ", bindings)} {r.Sql}";
    }

    private static string RenameList(Rename r, Schema input)
    {
        var map = r.Renamings.ToDictionary(x => input.Find(x.From)!.Value.Name, x => x.To);
        return string.Join(", ", input.Columns.Select(c =>
            map.TryGetValue(c.Name, out var to) ? $"{c.Name.Ident()} AS {to.Ident()}" : c.Name.Ident()));
    }

    private static string CastList(Cast cast, Schema input)
    {
        var target = input.Find(cast.Column)!.Value.Name;
        return string.Join(", ", input.Columns.Select(c =>
            c.Name == target ? $"CAST({c.Name.Ident()} AS {cast.Type.ToSqlType()}) AS {c.Name.Ident()}" : c.Name.Ident()));
    }

    private static string JoinBody(Join j, IReadOnlyList<string> inputs, Schema left, Schema right)
    {
        var on = string.Join(" AND ", j.Keys.Select(k => $"l.{k.Left.Ident()} = r.{k.Right.Ident()}"));
        var kind = j.Kind switch
        {
            JoinKind.Inner => "JOIN",
            JoinKind.Left => "LEFT JOIN",
            JoinKind.Right => "RIGHT JOIN",
            JoinKind.Full => "FULL JOIN",
            JoinKind.Semi => "SEMI JOIN",
            JoinKind.Anti => "ANTI JOIN",
            _ => throw new ArgumentOutOfRangeException(nameof(j)),
        };
        var leftColumns = left.Columns.Select(c => $"l.{c.Name.Ident()}");
        var rightColumns = j.Kind is JoinKind.Semi or JoinKind.Anti
            ? []
            : right.Columns.Select(c => left.Has(c.Name)
                ? $"r.{c.Name.Ident()} AS {(c.Name + SchemaInference.RightSuffix).Ident()}"
                : $"r.{c.Name.Ident()}");
        return $"SELECT {string.Join(", ", leftColumns.Concat(rightColumns))} FROM {inputs[0]} l {kind} {inputs[1]} r ON {on}";
    }

    private static string AggregateBody(Aggregate a, string input, Schema schema)
    {
        var groups = a.GroupBy.Select(g => schema.Find(g)!.Value.Name.Ident()).ToList();
        var aggregates = a.Aggregates.Select(x => $"{AggregateSql(x, schema)} AS {x.Name.Ident()}");
        var tail = groups.Count == 0 ? "" : $" GROUP BY {string.Join(", ", groups)} ORDER BY {string.Join(", ", groups)}";
        return $"SELECT {string.Join(", ", groups.Concat(aggregates))} FROM {input}{tail}";
    }

    /// <summary>Sums and averages are cast so the result type is predictable instead of DuckDB's HUGEINT.</summary>
    private static string AggregateSql(Aggregation x, Schema schema)
    {
        var column = x.Column is null ? "*" : schema.Find(x.Column)!.Value.Name.Ident();
        var type = x.Column is null ? ColumnType.Integer : schema.Find(x.Column)!.Value.Type;
        return x.Function switch
        {
            AggregateFunction.Count => $"count({column})",
            AggregateFunction.Sum => $"CAST(sum({column}) AS {type.ToSqlType()})",
            AggregateFunction.Avg => $"CAST(avg({column}) AS DOUBLE)",
            AggregateFunction.Min => $"min({column})",
            AggregateFunction.Max => $"max({column})",
            _ => throw new ArgumentOutOfRangeException(nameof(x)),
        };
    }
}
