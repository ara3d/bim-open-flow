using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using SchemaRules = BimOpenFlow.Relations.SchemaRules;
using SchemaInputs = BimOpenFlow.Relations.SchemaInputs;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Casts one column to a new type, in place or as a new column.
/// onError 'null' uses TRY_CAST and warns with the count of rows that became null.
/// Date and datetime casts accept ISO-8601 text only and come back as ISO text.</summary>
// TODO: CountNulledRows aligns input/output rows by index, relying on DuckDB's
// preserve_insertion_order default; count via SQL instead to remove the assumption.
public sealed class TableCastNode : ISchemaNode
{
    public const string Kind = "table.cast";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [new PortSpec("table", PortType.Table)],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params:
        [
            new ParamSpec("column", ParamKind.Text, Suggest: SuggestSource.ColumnsOf("table")),
            new ParamSpec("type", ParamKind.Enum, "",
                ["boolean", "integer", "number", "text", "date", "datetime"]),
            new ParamSpec("onError", ParamKind.Enum, "error", ["error", "null"]),
            new ParamSpec("name", ParamKind.Text),
        ],
        "Converts a column to boolean, integer, number, text, date, or datetime.",
        Schemas: [SchemaSpec.Inferred("table")]);

    private static readonly string[] Types = ["boolean", "integer", "number", "text", "date", "datetime"];

    private readonly record struct Args(string Column, string Type, string OnError, string Name)
    {
        public static Args Read(ParamValues parameters)
            => new(parameters.RequiredText("column", Kind),
                parameters.RequiredEnum("type", Kind, "", Types),
                parameters.RequiredEnum("onError", Kind, "error", "error", "null"),
                parameters.GetText("name"));

        /// <summary>The column type of the result. Date and datetime casts come back as ISO text, so
        /// they are Text here, not Date or Timestamp.</summary>
        public ColumnType OutputType => Type switch
        {
            "boolean" => ColumnType.Boolean,
            "integer" => ColumnType.Integer,
            "number" => ColumnType.Number,
            _ => ColumnType.Text,
        };
    }

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
        => SchemaInputs.Run(Spec, inputs, schemas =>
        {
            var args = Args.Read(parameters);
            return SchemaRules.Cast(schemas[0], args.Column, args.OutputType, args.Name.Length == 0 ? null : args.Name);
        }, throughDuckDb: true);

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var (columnParameter, type, onError, name) = Args.Read(parameters);
        var column = table.CanonicalName(columnParameter, Kind);
        if (name.Length > 0 && table.ColumnIndex(name) >= 0)
            throw new ArgumentException($"{Kind}: column '{name}' already exists.");

        var duckType = type switch
        {
            "boolean" => "BOOLEAN",
            "integer" => "BIGINT",
            "number" => "DOUBLE",
            "text" => "VARCHAR",
            "date" => "DATE",
            _ => "TIMESTAMP",
        };
        var cast = onError == "null" ? "TRY_CAST" : "CAST";
        var expr = $"{cast}({column.Ident()} AS {duckType})";
        var sql = name.Length == 0
            ? $"SELECT * REPLACE ({expr} AS {column.Ident()}) FROM t"
            : $"SELECT *, {expr} AS {name.Ident()} FROM t";
        var result = DuckTableSql.Run(Kind, table, sql);

        if (onError == "null")
        {
            var nulled = CountNulledRows(table, result, column, name.Length == 0 ? column : name);
            if (nulled > 0)
                context.Warn($"{Kind}: {nulled} value(s) could not be cast to {type} and became null.");
        }
        return [new TableValue(result)];
    }

    /// <summary>Rows where the source cell was present but the cast result is null.</summary>
    private static int CountNulledRows(IDataTable input, IDataTable output,
        string sourceColumn, string resultColumn)
    {
        var source = input.RequireColumn(sourceColumn, Kind);
        var target = output.RequireColumn(resultColumn, Kind);
        var nulled = 0;
        for (var row = 0; row < input.RowCount(); row++)
            if (input[source, row] is not (null or DBNull) && output[target, row] is null or DBNull)
                nulled++;
        return nulled;
    }
}
