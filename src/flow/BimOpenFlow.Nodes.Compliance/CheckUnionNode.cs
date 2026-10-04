using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Compliance;

/// <summary>
/// check.union: concatenates two verdict tables with identical column-name
/// sequences (rows of a, then rows of b). Chain unions to combine more tables.
/// NodeSpec cannot express variadic inputs, so the node takes exactly two.
/// </summary>
public sealed class CheckUnionNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "check.union", 1, NodeCapability.Pure,
        new[] { new PortSpec("a", PortType.Table), new PortSpec("b", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        Array.Empty<ParamSpec>(),
        "Concatenates two verdict tables with identical columns; chain for more.");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        var a = inputs.TableAt(0);
        var b = inputs.TableAt(1);
        a.RequireVerdictTable();
        b.RequireVerdictTable();
        RequireSameColumns(a, b);

        var countA = a.Rows.Count;
        var count = countA + b.Rows.Count;
        var columns = new MemoryColumn[a.Columns.Count];
        for (var c = 0; c < columns.Length; c++)
        {
            var cells = new object?[count];
            for (var r = 0; r < countA; r++)
                cells[r] = a.Cell(c, r);
            for (var r = countA; r < count; r++)
                cells[r] = b.Cell(c, r - countA);
            var descriptor = a.Columns[c].Descriptor;
            columns[c] = new MemoryColumn(descriptor.Name, descriptor.Type, cells, c);
        }
        return new FlowValue[] { new TableValue(new MemoryTable(a.Name, columns)) };
    }

    private static void RequireSameColumns(IDataTable a, IDataTable b)
    {
        var namesA = a.Columns.Select(c => c.Descriptor.Name).ToList();
        var namesB = b.Columns.Select(c => c.Descriptor.Name).ToList();
        if (!namesA.SequenceEqual(namesB))
            throw new ArgumentException(
                $"check.union inputs must have identical columns; got [{string.Join(", ", namesA)}] and [{string.Join(", ", namesB)}]");
    }
}
