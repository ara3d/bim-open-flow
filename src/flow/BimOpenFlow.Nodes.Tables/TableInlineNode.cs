using System.Text.Json;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Builds a small table from a JSON array of objects typed directly
/// into the node — rate cards, mappings, thresholds — without a side file.</summary>
public sealed class TableInlineNode : ISchemaNode
{
    public const string Kind = "table.inline";

    public NodeSpec Spec { get; } = new(
        Kind, 1, NodeCapability.Pure,
        Inputs: [],
        Outputs: [new PortSpec("table", PortType.Table)],
        Params: [new ParamSpec("rows", ParamKind.Json)],
        "Builds a table from a JSON array of objects, e.g. [{\"type\":\"Wall\",\"rate\":120.5}]. Column types are inferred (bool/integer/number/text); a column mixing value types is an error; nulls and missing keys are allowed.",
        Schemas: [SchemaSpec.Inferred("table")]);

    /// <summary>One parsed column: the evaluation builds a table from it and the shape rule reads its type.</summary>
    private sealed record ColumnData(string Name, Type Type, object?[] Cells);

    public SchemaResult Infer(string output, IReadOnlyList<SchemaInput> inputs, ParamValues parameters, ISchemaCatalog catalog)
    {
        try
        {
            var columns = Parse(parameters.RequiredText("rows", Kind)).Select(c =>
                new Column(c.Name, ToColumnType(c.Type), Nullable: c.Cells.Any(cell => cell is null)));
            return SchemaResult.Of(new Schema(columns.ToList()));
        }
        catch (ArgumentException e)
        {
            return SchemaResult.Fail(e.Message);
        }
    }

    private static ColumnType ToColumnType(Type type)
        => type == typeof(bool) ? ColumnType.Boolean
            : type == typeof(long) ? ColumnType.Integer
            : type == typeof(double) ? ColumnType.Number
            : ColumnType.Text;

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var builder = new DataTableBuilder("inline");
        foreach (var column in Parse(parameters.RequiredText("rows", Kind)))
            builder.AddColumn(column.Cells, column.Name, column.Type);
        return [new TableValue(builder.Build())];
    }

    private static IReadOnlyList<ColumnData> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return Columns(document.RootElement);
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"{Kind}: 'rows' is not valid JSON: {e.Message}", e);
        }
    }

    private static IReadOnlyList<ColumnData> Columns(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new ArgumentException($"{Kind}: 'rows' must be a JSON array of objects.");

        var names = new List<string>();
        var columns = new Dictionary<string, List<object?>>();
        var rowCount = 0;
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ArgumentException($"{Kind}: row {rowCount} is not a JSON object.");
            foreach (var property in element.EnumerateObject())
            {
                if (!columns.TryGetValue(property.Name, out var cells))
                {
                    cells = [.. Enumerable.Repeat<object?>(null, rowCount)];
                    columns[property.Name] = cells;
                    names.Add(property.Name);
                }
                if (cells.Count > rowCount)
                    throw new ArgumentException($"{Kind}: row {rowCount} repeats key '{property.Name}'.");
                cells.Add(Value(property.Value, property.Name));
            }
            rowCount++;
            foreach (var cells in columns.Values)
                if (cells.Count < rowCount)
                    cells.Add(null);
        }

        return names.Select(name =>
        {
            var type = CellType(columns[name], name);
            var cells = type == typeof(double)
                ? columns[name].Select(c => c is long l ? (object?)(double)l : c).ToArray()
                : columns[name].ToArray();
            return new ColumnData(name, type, cells);
        }).ToList();
    }

    private static object? Value(JsonElement value, string column)
        => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var i) ? i : (object)value.GetDouble(),
            _ => throw new ArgumentException(
                $"{Kind}: column '{column}' has a nested {value.ValueKind}; values must be scalars."),
        };

    /// <summary>One value type per column, or an error — with a single numeric
    /// exception: integers and numbers in one column widen to number, because
    /// JSON has one number type and "120" next to "120.5" is the same column
    /// (the rate-card shape). Everything else mixing is an error.</summary>
    private static Type CellType(IReadOnlyList<object?> cells, string name)
    {
        var types = cells.Where(c => c != null).Select(c => c!.GetType()).Distinct().ToList();
        return types.Count switch
        {
            0 => typeof(string),
            1 => types[0],
            2 when types.Contains(typeof(long)) && types.Contains(typeof(double)) => typeof(double),
            _ => throw new ArgumentException(
                $"{Kind}: column '{name}' mixes value types ({string.Join(", ", types.Select(TypeName))})."),
        };
    }

    private static string TypeName(Type type)
        => type == typeof(long) ? "integer"
            : type == typeof(double) ? "number"
            : type == typeof(bool) ? "boolean"
            : "text";
}
