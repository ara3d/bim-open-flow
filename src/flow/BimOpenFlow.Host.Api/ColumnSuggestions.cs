using Ara3D.BimOpenSchema;
using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Relations;
using BimOpenFlow.Contracts;
using ColumnType = Ara3D.DataFlowEngine.Abstractions.ColumnType;
using Column = Ara3D.DataFlowEngine.Abstractions.Column;
using EngineColumnUse = Ara3D.DataFlowEngine.Abstractions.ColumnUse;

namespace BimOpenFlow.Host.Api;

/// <summary>One column a picker can offer: where its type came from (the schema pass or a recorded
/// result) does not matter here. Kind is the column kind's name when the pass declared one.</summary>
internal readonly record struct SuggestedColumn(string Name, ColumnType Type, string? Kind);

/// <summary>Turns the columns of an input into suggestions ordered and trimmed for what the parameter
/// declares it is for (<see cref="EngineColumnUse"/>), each with its type and, for a registered kind,
/// the kind's meaning and role. The relevance rules live here, driven by the catalog, so no editor
/// has to know which node wants which columns.</summary>
internal static class ColumnSuggestions
{
    /// <summary>The leading entry of an Aggregate list: the whole row, which only count accepts.</summary>
    public const string RowEntry = "*";

    private static readonly IReadOnlyList<ColumnType> AllTypes = Enum.GetValues<ColumnType>();

    private static readonly IReadOnlyDictionary<ColumnType, IReadOnlyList<string>> FunctionsByType =
        AllTypes.ToDictionary(type => type, FunctionsAllowedOn);

    /// <summary>The columns as suggestions. <paramref name="other"/> is the input a join key is compared with.</summary>
    public static IReadOnlyList<Suggestion> Build(IReadOnlyList<SuggestedColumn> columns, EngineColumnUse use,
        IReadOnlyList<SuggestedColumn>? other)
    {
        var shown = columns
            .Where(c => use != EngineColumnUse.Value || IsNumeric(c.Type))
            .OrderBy(c => Rank(c, use, other))
            .Select(c => ToSuggestion(c, use))
            .ToList();
        return use == EngineColumnUse.Aggregate ? [new Suggestion(RowEntry, "rows", null, "Every row of the table.", null, ["count"]), .. shown] : shown;
    }

    private static Suggestion ToSuggestion(SuggestedColumn column, EngineColumnUse use)
    {
        var kind = column.Kind is null ? null : ColumnKinds.Find(column.Kind);
        return new(column.Name, column.Type.ToString(), kind?.Name, kind?.Meaning, kind?.Role.ToString(),
            use == EngineColumnUse.Aggregate ? FunctionsByType[column.Type] : null);
    }

    private static bool IsNumeric(ColumnType type)
        => type is ColumnType.Integer or ColumnType.Number;

    private static int Rank(SuggestedColumn column, EngineColumnUse use, IReadOnlyList<SuggestedColumn>? other)
    {
        var role = column.Kind is null ? null : ColumnKinds.Find(column.Kind)?.Role;
        return use switch
        {
            EngineColumnUse.Label => role is ColumnRole.Label or ColumnRole.Category ? 0 : 1,
            EngineColumnUse.Key => role is ColumnRole.Key ? 0 : 1,
            EngineColumnUse.JoinKey => (IsShared(column, other) ? 0 : 2) + (role is ColumnRole.Key ? 0 : 1),
            _ => 0,
        };
    }

    private static bool IsShared(SuggestedColumn column, IReadOnlyList<SuggestedColumn>? other)
        => other?.Any(o => string.Equals(o.Name, column.Name, StringComparison.OrdinalIgnoreCase)) == true;

    /// <summary>Asks the relation layer's aggregate rule which functions accept a column of this type, so
    /// the table of allowed functions has one home (<see cref="SchemaRules.Aggregate"/>).</summary>
    private static IReadOnlyList<string> FunctionsAllowedOn(ColumnType type)
    {
        var input = new Schema([new Column("c", type)]);
        return Enum.GetValues<AggregateFunction>()
            .Where(f => SchemaRules.Aggregate(input, [], [new Aggregation(f, f == AggregateFunction.Count ? null : "c", "r")]).Ok)
            .Select(f => f.ToString().ToLowerInvariant())
            .ToList();
    }
}
