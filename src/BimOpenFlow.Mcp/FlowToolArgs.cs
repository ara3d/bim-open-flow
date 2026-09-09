using Ara3D.MCP;

namespace BimOpenFlow.Mcp;

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

    public static McpSchemaBuilder Paged(this McpSchemaBuilder builder)
        => builder
            .Integer("skip", "Number of rows to skip. Default 0.")
            .Integer("take", $"Maximum rows to return. Default {DefaultTake}, capped at {MaxTake}.");

    public static int Skip(this McpToolArgs args)
        => Math.Max(args.GetInt("skip") ?? 0, 0);

    public static int Take(this McpToolArgs args)
        => Math.Clamp(args.GetInt("take") ?? DefaultTake, 1, MaxTake);
}
