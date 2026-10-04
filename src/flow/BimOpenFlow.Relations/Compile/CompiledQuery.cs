namespace BimOpenFlow.Relations;

public enum SourceKind { Csv, Table }

/// <summary>A source the SQL refers to symbolically as "source"."reference". The executor
/// binds each one through the connection registry before running the statement.</summary>
public readonly record struct SourceUse(string Source, SourceKind Kind, string Reference)
{
    public string Ident => Source.Ident() + "." + Reference.Ident();
}

/// <summary>An in-process table the SQL refers to as "_inline"."Name"; the executor writes
/// the table found under Hash into that schema before running the statement.</summary>
public readonly record struct InlineUse(string Name, string Hash)
{
    public string Ident => CompiledQuery.InlineSchema.Ident() + "." + Name.Ident();
}

/// <summary>One statement (WITH n1 AS ..., nK AS ... SELECT * FROM nK) plus the sources and
/// inline tables it binds.</summary>
public sealed record CompiledQuery(string Sql, IReadOnlyList<SourceUse> Sources, IReadOnlyList<InlineUse> Inlines)
{
    public const string InlineSchema = "_inline";

    public CompiledQuery(string sql, IReadOnlyList<SourceUse> sources) : this(sql, sources, []) { }

    public string WithLimit(long limit, long offset = 0)
        => $"SELECT * FROM ({Sql}) AS _q LIMIT {limit}" + (offset > 0 ? $" OFFSET {offset}" : "");

    public string CountSql
        => $"SELECT count(*) FROM ({Sql}) AS _q";
}
