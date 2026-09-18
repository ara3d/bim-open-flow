namespace BimOpenFlow.Relations;

/// <summary>A CSV file under the file root that the source name resolves to.</summary>
public sealed class ReadCsv(string source, string path) : Plan
{
    public string Source => source;
    public string Path => path;
    public override IReadOnlyList<Plan> Inputs => [];
}

/// <summary>A table in the database that the source name resolves to.</summary>
public sealed class ReadTable(string source, string table) : Plan
{
    public string Source => source;
    public string Table => table;
    public override IReadOnlyList<Plan> Inputs => [];
}

/// <summary>The escape hatch: user-written SQL whose inputs are visible as t1, t2, ... tN.
/// Only a single SELECT or WITH statement is representable.</summary>
public sealed class RawSql(string sql, IReadOnlyList<Plan> inputs) : Plan
{
    public string Sql { get; } = SqlText.RequireReadOnly(sql);
    public override IReadOnlyList<Plan> Inputs => inputs;
}
