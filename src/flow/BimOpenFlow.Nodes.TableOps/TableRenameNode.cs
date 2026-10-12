using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;
using Renaming = BimOpenFlow.Relations.Renaming;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Renames columns via comma-separated old=new pairs. Unknown old names
/// warn and are skipped; a new name colliding with a remaining column errors.</summary>
public sealed class TableRenameNode : ISchemaNode
{
    public const string Kind = "table.rename";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("renames", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table"), List: true)],
        "Renames columns using comma-separated 'old=new' pairs.",
        Schemas: [SchemaSpec.Inferred("table")]);

    private static IReadOnlyList<Renaming> ReadRenamings(ParamValues parameters)
        => parameters.RequiredText("renames", Kind).SplitNames().Select(entry =>
        {
            var parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
            return parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0
                ? throw new ArgumentException($"{Kind}: cannot parse rename '{entry}' (expected old=new).")
                : new Renaming(parts[0], parts[1]);
        }).ToList();

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas => SchemaRules.Rename(schemas[0], ReadRenamings(parameters), skipMissing: true), throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, target) in ReadRenamings(parameters))
        {
            if (table.ColumnIndex(source) < 0)
            {
                context.Warn($"{Kind}: no column named '{source}'; rename skipped.");
                continue;
            }
            if (!renames.TryAdd(table.CanonicalName(source, Kind), target))
                throw new ArgumentException($"{Kind}: column '{source}' is renamed more than once.");
        }

        var finalNames = table.Names()
            .Select(n => renames.TryGetValue(n, out var renamed) ? renamed : n).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in finalNames)
            if (!seen.Add(name))
                throw new ArgumentException($"{Kind}: renaming produces duplicate column '{name}'.");

        var terms = table.Names().Zip(finalNames,
            (old, final) => old == final ? old.Ident() : $"{old.Ident()} AS {final.Ident()}");
        return [new TableValue(DuckTableSql.Run(Kind, table, $"SELECT {string.Join(", ", terms)} FROM t"))];
    }
}
