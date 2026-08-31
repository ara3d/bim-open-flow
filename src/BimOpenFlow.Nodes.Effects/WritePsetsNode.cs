using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Ara3D.Ifc.Tests;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>
/// sink.writePsets: applies byte-exact property-set additions to a copy of an IFC file.
/// Input rows (entityId, psetName, paramName, paramValue) are grouped by (entityId, psetName)
/// in first-appearance order; each group becomes one IfcPropertySet attached to the entity.
/// v1 limitation: every value is written as IFCTEXT; typed measures/units come later.
/// </summary>
public sealed class WritePsetsNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "sink.writePsets", 1, NodeCapability.Effect,
        new[] { new PortSpec("in", PortType.Table) },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("sourcePath", ParamKind.FilePath),
            new ParamSpec("targetPath", ParamKind.FilePath),
        },
        "Byte-exact pset write-back: reads sourcePath, appends psets from the input table, writes targetPath. Outputs a one-row summary (targetPath, entitiesTouched, valuesWritten).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Spec.Kind);
        var table = inputs.TableAt(0);
        var sourcePath = parameters.RequiredPath("sourcePath");
        var targetPath = parameters.RequiredPath("targetPath");
        var groups = GroupRows(table);

        using var source = IfcSourceFile.Load(sourcePath);
        var ownerHistoryId = source.FirstIdOfType("IFCOWNERHISTORY");
        if (ownerHistoryId < 0)
            throw new ArgumentException($"'{sourcePath}' has no IFCOWNERHISTORY entity");

        var builder = new IfcPropertySetBuilder(source.MaxId + 1, ownerHistoryId);
        var entities = new HashSet<long>();
        var valuesWritten = 0L;
        foreach (var group in groups)
        {
            if (!source.Contains(checked((int)group.EntityId)))
                throw new ArgumentException($"'{sourcePath}' has no entity #{group.EntityId}");
            builder.AddPropertySet(
                (int)group.EntityId, group.PsetName, group.Values, $"{group.EntityId}:{group.PsetName}");
            entities.Add(group.EntityId);
            valuesWritten += group.Values.Count;
        }

        var bytes = IfcPatcher.Append(source, builder.Lines);
        var dir = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(targetPath, bytes);

        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("writePsets",
                ("targetPath", targetPath),
                ("entitiesTouched", (long)entities.Count),
                ("valuesWritten", valuesWritten))),
        };
    }

    private sealed record PsetGroup(long EntityId, string PsetName, List<IfcPropertyValue> Values);

    /// <summary>Rows grouped by (entityId, psetName), groups and values in first-appearance order.</summary>
    private static IReadOnlyList<PsetGroup> GroupRows(IDataTable table)
    {
        var entityId = table.RequiredColumn("entityId");
        var psetName = table.RequiredColumn("psetName");
        var paramName = table.RequiredColumn("paramName");
        var paramValue = table.RequiredColumn("paramValue");
        var groups = new List<PsetGroup>();
        var byKey = new Dictionary<(long, string), PsetGroup>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var id = IntegerCell(table, entityId, r);
            var pset = TextCell(table, psetName, r);
            var key = (id, pset);
            if (!byKey.TryGetValue(key, out var group))
            {
                group = new PsetGroup(id, pset, new List<IfcPropertyValue>());
                byKey.Add(key, group);
                groups.Add(group);
            }
            group.Values.Add(IfcPropertyValue.Text(TextCell(table, paramName, r), TextCell(table, paramValue, r)));
        }
        return groups;
    }

    private static long IntegerCell(IDataTable table, int column, int row)
        => table[column, row] switch
        {
            long v => v,
            int v => v,
            _ => throw new ArgumentException(
                $"Column '{table.Columns[column].Descriptor.Name}' row {row} must be a non-null Integer"),
        };

    private static string TextCell(IDataTable table, int column, int row)
        => table[column, row] as string
           ?? throw new ArgumentException(
               $"Column '{table.Columns[column].Descriptor.Name}' row {row} must be non-null Text");
}
