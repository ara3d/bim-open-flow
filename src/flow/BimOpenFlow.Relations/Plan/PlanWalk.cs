namespace BimOpenFlow.Relations;

/// <summary>Traversal helpers so the other layers walk a plan without knowing its shape.</summary>
public static class PlanWalk
{
    /// <summary>Every node in the tree, inputs before the nodes that use them, each distinct plan once.</summary>
    public static IReadOnlyList<Plan> PostOrder(this Plan plan)
    {
        var seen = new HashSet<Plan>();
        var order = new List<Plan>();
        Visit(plan);
        return order;

        void Visit(Plan p)
        {
            if (!seen.Add(p)) return;
            foreach (var input in p.Inputs) Visit(input);
            order.Add(p);
        }
    }

    public static IReadOnlyList<Plan> Sources(this Plan plan)
        => plan.PostOrder().Where(p => p is ReadCsv or ReadTable).ToList();
}
