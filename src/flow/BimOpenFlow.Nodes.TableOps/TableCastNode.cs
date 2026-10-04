using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.TableOps;

/// <summary>Casts one column to a new type, in place or as a new column.
/// onError 'null' uses TRY_CAST and warns with the count of rows that became null.
/// Date and datetime casts accept ISO-8601 text only and come back as ISO text.</summary>
// TODO: CountNulledRows aligns input/output rows by index, relying on DuckDB's
// preserve_insertion_order default; count via SQL instead to remove the assumption.
public sealed class TableCastNode : IFlowNode
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
        "Converts a column to boolean, integer, number, text, date, or datetime.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context,
        IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var table = inputs.TableInput(0, Kind);
        var column = table.CanonicalName(parameters.RequiredText("column", Kind), Kind);
        var type = parameters.RequiredEnum("type", Kind, "",
            "boolean", "integer", "number", "text", "date", "datetime");
        var onError = parameters.RequiredEnum("onError", Kind, "error", "error", "null");
        var name = parameters.GetText("name");
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
