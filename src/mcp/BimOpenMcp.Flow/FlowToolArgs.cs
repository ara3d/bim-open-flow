using System.Text.Json;
using Ara3D.MCP;

namespace BimOpenMcp.Flow;

/// <summary>The argument and schema fragments the BimOpenFlow tools share.</summary>
internal static class FlowToolArgs
{
    public const int DefaultTake = 100;
    public const int MaxTake = 1000;

    public static McpSchemaBuilder Analysis()
        => McpSchema.Object().String("id", "Analysis id (a lowercase slug, e.g. 'wall-areas').", required: true);

    /// <summary>'id', or 'analysisId' as an alias, because agents reach for both.</summary>
    public static string AnalysisId(this McpToolArgs args)
        => args.GetString("id") is { Length: > 0 } id
            ? id
            : args.GetString("analysisId") is { Length: > 0 } alias
                ? alias
                : args.GetRequiredString("id");

    /// <summary>The optional 'graphInputs' argument: values for the analysis's graph inputs.</summary>
    public static McpSchemaBuilder GraphInputs(this McpSchemaBuilder builder, string effect)
        => builder.String("graphInputs",
            "Values for the graph's inputs (see getAnalysis 'inputs'), as a JSON object in a string: "
            + "{\"model\": \"C:/data/other.duckdb\"}. They become the analysis's current values, kept "
            + "until changed, and " + effect + " An input not named keeps its current value.");

    /// <summary>Parses the 'graphInputs' string into name to canonical text; null when absent or empty.</summary>
    public static IReadOnlyDictionary<string, string>? GraphInputs(this McpToolArgs args)
    {
        var text = args.GetString("graphInputs");
        if (string.IsNullOrWhiteSpace(text))
            return null;
        try
        {
            using var parsed = JsonDocument.Parse(text);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("'graphInputs' must be a JSON object of input name to value");
            return parsed.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString()!,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => p.Value.GetRawText(),
                _ => throw new ArgumentException($"Graph input '{p.Name}' must be a string, number or boolean"),
            });
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"'graphInputs' is not valid JSON: {e.Message}", e);
        }
    }

    public static McpSchemaBuilder Paged(this McpSchemaBuilder builder)
        => builder
            .Integer("skip", "Number of rows to skip. Default 0.")
            .Integer("take", $"Maximum rows to return. Default {DefaultTake}, capped at {MaxTake}.");

    public static int Skip(this McpToolArgs args)
        => Math.Max(args.GetInt("skip") ?? 0, 0);

    public static int Take(this McpToolArgs args)
        => Math.Clamp(args.GetInt("take") ?? DefaultTake, 1, MaxTake);
}
