namespace BimOpenFlow.Nodes.Relations;

/// <summary>The rel.* pack, built over one shared runtime.</summary>
public static class RelationNodes
{
    public static IReadOnlyList<IFlowNode> All(RelationRuntime runtime) =>
    [
        new RelCsvNode(runtime),
        new RelTableNode(runtime),
        new RelFromTableNode(runtime),
        new RelSqlNode(runtime),
        new RelSelectNode(runtime),
        new RelFilterNode(runtime),
        new RelDeriveNode(runtime),
        new RelSortNode(runtime),
        new RelLimitNode(runtime),
        new RelAggregateNode(runtime),
        new RelJoinNode(runtime),
        new RelMaterializeNode(runtime),
    ];
}
