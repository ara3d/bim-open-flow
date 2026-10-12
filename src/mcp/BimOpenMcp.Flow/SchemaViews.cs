using BimOpenFlow.Contracts;

namespace BimOpenMcp.Flow;

/// <summary>The schema pass as Claude reads it in getAnalysis: one entry per Table or Relation output,
/// columns as "Name:Type", and a field only when it has something to say. Column kinds and meanings stay in
/// getNodeCatalog, which is where a declaration is documented once.</summary>
public static class SchemaViews
{
    public static IReadOnlyList<object> Of(IReadOnlyList<PortSchema> ports)
        => ports.Select(View).ToList();

    private static object View(PortSchema port)
    {
        var view = new Dictionary<string, object> { ["node"] = port.Node, ["output"] = port.Output };
        if (port.Status != SchemaStatus.Ok)
            view["status"] = port.Status.ToString().ToLowerInvariant();
        if (port.Columns is not null)
            view["columns"] = port.Columns.Select(c => $"{c.Name}:{c.Type}").ToList();
        if (port.Reason is not null)
            view["reason"] = port.Reason;
        if (port.Errors.Count > 0)
            view["errors"] = port.Errors;
        if (port.Warnings.Count > 0)
            view["warnings"] = port.Warnings;
        return view;
    }
}
