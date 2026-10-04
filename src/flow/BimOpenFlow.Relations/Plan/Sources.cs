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

/// <summary>An in-process table registered with the executor under its content hash. The plan
/// carries the table's identity and schema; the rows live in the runtime's inline store.
/// <see cref="TableHash"/> is the rows' identity, distinct from the inherited plan hash.</summary>
public sealed class InlineTable(string name, string hash, Schema schema) : Plan
{
    public string Name => name;
    public string TableHash => hash;
    public Schema Schema => schema;
    public override IReadOnlyList<Plan> Inputs => [];
}

/// <summary>The escape hatch: user-written SQL whose inputs are visible as t1, t2, ... tN.
/// Only a single SELECT or WITH statement is representable.</summary>
public sealed class RawSql(string sql, IReadOnlyList<Plan> inputs) : Plan
{
    public string Sql { get; } = SqlText.RequireReadOnly(sql);
    public override IReadOnlyList<Plan> Inputs => inputs;
}
