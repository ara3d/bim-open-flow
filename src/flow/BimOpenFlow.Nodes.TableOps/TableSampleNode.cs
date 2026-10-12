using System.Globalization;
using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Seeded deterministic sampling: a fixed row count (reservoir) or a
/// per-row probability (bernoulli). Sampled rows keep their input order.</summary>
public sealed class TableSampleNode : ISchemaNode
{
    public const string Kind = "table.sample";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("mode", ParamKind.Enum, "rows", ["rows", "fraction"]),
            new ParamSpec("rows", ParamKind.Integer, "100"),
            new ParamSpec("fraction", ParamKind.Number, "0.1"),
            new ParamSpec("seed", ParamKind.Integer, "1"),
        ],
        "Takes a seeded random sample: a fixed number of rows, or a fraction of them.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>The DuckDB sample clause and the seed, from checked parameters; evaluation and the shape rule both read them here.</summary>
    private static (string Sample, long Seed) ReadSample(ParamValues parameters)
    {
        var mode = parameters.RequiredEnum("mode", Kind, "rows", "rows", "fraction");
        var seed = parameters.GetInteger("seed", 1);
        if (mode == "rows")
        {
            var rows = parameters.GetInteger("rows", 100);
            return rows < 0
                ? throw new ArgumentException($"{Kind}: parameter 'rows' must be a non-negative integer.")
                : ($"reservoir({rows} ROWS)", seed);
        }
        var fraction = parameters.GetNumber("fraction", 0.1);
        if (fraction is < 0 or > 1 || double.IsNaN(fraction))
            throw new ArgumentException($"{Kind}: parameter 'fraction' must be between 0 and 1.");
        return ($"bernoulli({(fraction * 100).ToString("R", CultureInfo.InvariantCulture)}%)", seed);
    }

    /// <summary>Sampling keeps every column and its type.</summary>
    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            ReadSample(parameters);
            return SchemaResult.Of(schemas[0]);
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var (sample, seed) = ReadSample(parameters);
        var ord = TableColumns.FreeName("__row__", table);
        var cols = string.Join(", ", table.Names().Select(DuckTableSql.QuoteIdent));
        var sql = $"""
            SELECT {cols} FROM (
              SELECT * FROM t USING SAMPLE {sample} REPEATABLE ({seed}))
            ORDER BY {ord.Ident()}
            """;
        return [new TableValue(DuckTableSql.Run(Kind, table.WithOrdinal(ord), sql))];
    }
}
