using System.Text.RegularExpressions;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.DataFlowEngine.Abstractions;

namespace BimOpenFlow.Nodes.DuckDb;

/// <summary>SQL over one to four flowing tables. Connected inputs load into an
/// in-memory DuckDB as t1..t4 (t aliases t1); t2..t4 are optional ports, so a
/// single-table query needs only t1 connected.</summary>
public sealed partial class SqlQueryNode : ISchemaNode
{
    public const string Kind = "sql.query";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs:
        [
            new PortSpec("t1", PortType.Table),
            new PortSpec("t2", PortType.Table, Optional: true),
            new PortSpec("t3", PortType.Table, Optional: true),
            new PortSpec("t4", PortType.Table, Optional: true),
        ],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("sql", ParamKind.Text)],
        "Runs one read-only SQL query over the connected input tables t1..t4 (t = t1).",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>DESCRIBE over empty typed views of the inputs. An input whose own schema is unknown
    /// makes this one unknown, naming it. An optional port left unwired between two wired ones is
    /// an empty view, so a query that reads it is typed here although it fails when run.</summary>
    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaGuards.Guard(() =>
        {
            var sql = parameters.ReadOnlySql(Kind);
            var schemas = new List<Schema>();
            for (var i = 0; i < inputs.Count; i++)
            {
                if (!inputs[i].Connected && i > 0)
                {
                    schemas.Add(new Schema([]));
                    continue;
                }
                if (inputs[i].Schema is not { Ok: true } known)
                    return SchemaResult.Fail($"{Kind}: input t{i + 1} has no known columns"
                        + (inputs[i].Schema is { } bad ? $" ({bad})." : " (it is not connected)."));
                schemas.Add(known.Schema!);
            }
            while (schemas.Count > 1 && !inputs[schemas.Count - 1].Connected)
                schemas.RemoveAt(schemas.Count - 1);
            return DuckSchemas.OnTheWire(catalog.QuerySchema(WithAlias(sql), schemas));
        });

    /// <summary>The query with the CTE <c>t AS (SELECT * FROM t1)</c> in front, which the run provides as a view.</summary>
    private static string WithAlias(string sql)
    {
        const string alias = "t AS (SELECT * FROM t1)";
        var with = WithKeyword().Match(sql);
        return with.Success
            ? $"{with.Value}{alias}, {sql[with.Length..]}"
            : $"WITH {alias} {sql}";
    }

    [GeneratedRegex(@"^\s*WITH\s+(RECURSIVE\s+)?", RegexOptions.IgnoreCase)]
    private static partial Regex WithKeyword();

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var validated = parameters.ReadOnlySql(Kind);
        using var conn = BosDuckDb.OpenInMemory();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (i > 0 && inputs[i] is MissingValue)
                continue;
            if (inputs[i] is not TableValue t)
                throw new ArgumentException($"{Kind}: input t{i + 1} must be a Table.");
            conn.WriteTable(t.Table, $"t{i + 1}");
        }
        conn.Execute("CREATE VIEW t AS SELECT * FROM t1");
        return [new TableValue(conn.Query(validated, "query").NormalizeDatesToText())];
    }
}
