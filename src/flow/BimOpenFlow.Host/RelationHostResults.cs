using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Api;
using BimOpenFlow.Nodes.Relations;
using BimOpenFlow.Relations;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>Answers the API's relation questions from the rel.* pack's runtime: a page of
/// rows is one limited query plus a count, and columns come from the inferred schema alone.</summary>
public sealed class RelationHostResults(RelationRuntime runtime) : IRelationResults
{
    public TableSlice Slice(RelationValue relation, int skip, int take)
    {
        var plan = PlanOf(relation);
        var page = runtime.Materialize(plan, Math.Max(take, 0), Math.Max(skip, 0)).ToSlice(0, take);
        return page with { TotalRows = checked((int)runtime.Count(plan)), Skip = Math.Max(skip, 0) };
    }

    public IReadOnlyList<Suggestion> Columns(RelationValue relation)
        => runtime.Schema(PlanOf(relation)).Require().Columns
            .Select(c => new Suggestion(c.Name, c.Type.ToString()))
            .ToList();

    public IReadOnlyList<string> SourceFiles(RelationValue relation)
    {
        try
        {
            return relation.Payload switch
            {
                ReadTable table when runtime.Registry.Resolve(table.Source) is { Type: SourceType.DuckDbFile } location
                    => [location.Path],
                ReadCsv csv when runtime.Registry.Resolve(csv.Source) is { Type: SourceType.FileRoot } location
                    && File.Exists(Path.Combine(location.Path, csv.Path))
                    => [Path.Combine(location.Path, csv.Path)],
                _ => [],
            };
        }
        catch (SourcePreparingException)
        {
            return [];
        }
    }

    private static Plan PlanOf(RelationValue relation)
        => relation.Payload as Plan
           ?? throw new InvalidOperationException("The relation carries no in-process plan; it was read from a record, not evaluated here.");
}
