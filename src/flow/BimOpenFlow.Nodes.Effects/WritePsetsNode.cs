using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Ara3D.Ifc.Tests;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>
/// sink.writePsets: applies byte-exact property-set additions to a copy of an IFC file.
/// Input rows (entityId, psetName, paramName, paramValue) are grouped by (entityId, psetName)
/// in first-appearance order; each group becomes one IfcPropertySet attached to the entity.
/// An optional valueType column picks the IFC value type per row (see <see cref="PsetValueTypes"/>);
/// without it every value is IFCTEXT, as before. Units are still not written.
/// <para>Two optional inputs write zones. <c>zones</c> rows (zoneName, spaceId) become one IfcZone per
/// name, in first-appearance order, with an IfcRelAssignsToGroup listing the spaces' STEP ids.
/// <c>zonePsets</c> rows (zoneName, psetName, paramName, paramValue, optional valueType) attach property
/// sets to those zones by name, because a zone's STEP id does not exist until the Run allocates it.
/// Zones and their sets are appended after the entity sets, so the ids of the latter do not move
/// when zones are added.</para>
/// </summary>
public sealed class WritePsetsNode : IFlowNode
{
    public NodeSpec Spec { get; } = new(
        "sink.writePsets", 1, NodeCapability.Effect,
        new[]
        {
            new PortSpec("in", PortType.Table),
            new PortSpec("zones", PortType.Table, Optional: true),
            new PortSpec("zonePsets", PortType.Table, Optional: true),
        },
        new[] { new PortSpec("out", PortType.Table) },
        new[]
        {
            new ParamSpec("sourcePath", ParamKind.FilePath, Suggest: SuggestSource.FilesWith(".ifc")),
            new ParamSpec("targetPath", ParamKind.FilePath),
        },
        "Byte-exact pset write-back: reads sourcePath, appends psets from the input table, writes targetPath. "
        + "An optional valueType column names the IFC value type of each row - Text, Label, Identifier, Integer, "
        + "Number (Real is a synonym), or Boolean, case-insensitive - and paramValue is parsed from text with the "
        + "invariant culture; an empty cell or an absent column means Text. "
        + "Optional input zones (zoneName, spaceId) appends one IfcZone per name grouping those spaces; optional "
        + "input zonePsets (zoneName, psetName, paramName, paramValue, optional valueType) attaches property "
        + "sets to the zones by name. "
        + "Outputs a one-row summary (targetPath, entitiesTouched, valuesWritten, zonesWritten).");

    public IReadOnlyList<FlowValue> Eval(IEvalContext context, IReadOnlyList<FlowValue> inputs, ParamValues parameters)
    {
        context.RequireRun(Spec.Kind);
        var table = inputs.TableAt(0);
        var sourcePath = parameters.RequiredPath("sourcePath");
        var targetPath = parameters.RequiredPath("targetPath");
        var groups = GroupRows(table, "entityId", IntegerKey);
        var zoneSpaces = ReadZones(inputs.OptionalTableAt(1));
        var zoneGroups = inputs.OptionalTableAt(2) is { } zonePsets
            ? GroupRows(zonePsets, "zoneName", TextKey)
            : [];

        using var source = IfcSourceFile.Load(sourcePath);
        var ownerHistoryId = source.FirstIdOfType("IFCOWNERHISTORY");
        if (ownerHistoryId < 0)
            throw new ArgumentException($"'{sourcePath}' has no IFCOWNERHISTORY entity");

        var builder = new IfcPropertySetBuilder(source.MaxId + 1, ownerHistoryId);
        var entities = new HashSet<long>();
        var valuesWritten = 0L;
        foreach (var group in groups)
        {
            var id = (long)group.Key;
            if (!source.Contains(checked((int)id)))
                throw new ArgumentException($"'{sourcePath}' has no entity #{id}");
            builder.AddPropertySet((int)id, group.PsetName, group.Values, $"{id}:{group.PsetName}");
            entities.Add(id);
            valuesWritten += group.Values.Count;
        }

        var lines = new List<string>(builder.Lines);
        var zonesWritten = 0L;
        if (zoneSpaces.Count > 0)
        {
            var zoneBuilder = new IfcZoneBuilder(builder.NextId, ownerHistoryId, source.Schema);
            var zoneIds = new Dictionary<string, int>();
            foreach (var (name, spaceIds) in zoneSpaces)
            {
                foreach (var spaceId in spaceIds)
                    if (!source.Contains(spaceId))
                        throw new ArgumentException($"'{sourcePath}' has no space #{spaceId} for zone '{name}'");
                zoneIds.Add(name, zoneBuilder.AddZone(name, spaceIds));
            }
            zonesWritten = zoneIds.Count;

            var zonePsetBuilder = new IfcPropertySetBuilder(zoneBuilder.NextId, ownerHistoryId);
            foreach (var group in zoneGroups)
            {
                var name = (string)group.Key;
                if (!zoneIds.TryGetValue(name, out var zoneId))
                    throw new ArgumentException($"zonePsets names zone '{name}', which the zones input does not define");
                zonePsetBuilder.AddPropertySet(zoneId, group.PsetName, group.Values, $"zone:{name}:{group.PsetName}");
                entities.Add(zoneId);
                valuesWritten += group.Values.Count;
            }
            lines.AddRange(zoneBuilder.Lines);
            lines.AddRange(zonePsetBuilder.Lines);
        }
        else if (zoneGroups.Count > 0)
            throw new ArgumentException("zonePsets has rows but the zones input defines no zone");

        var bytes = IfcPatcher.Append(source, lines);
        var dir = Path.GetDirectoryName(Path.GetFullPath(targetPath));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllBytes(targetPath, bytes);

        return new FlowValue[]
        {
            new TableValue(Sinks.SummaryRow("writePsets",
                ("targetPath", targetPath),
                ("entitiesTouched", (long)entities.Count),
                ("valuesWritten", valuesWritten),
                ("zonesWritten", zonesWritten))),
        };
    }

    /// <summary>One property set to write: the target (a STEP id as long, or a zone name as string),
    /// the set's name, and its values.</summary>
    private sealed record PsetGroup(object Key, string PsetName, List<IfcPropertyValue> Values);

    private static object IntegerKey(IDataTable table, int column, int row)
        => IntegerCell(table, column, row);

    private static object TextKey(IDataTable table, int column, int row)
        => TextCell(table, column, row);

    /// <summary>Zone names with their space ids, in first-appearance order; empty without a zones input.</summary>
    private static IReadOnlyList<(string Name, List<int> SpaceIds)> ReadZones(IDataTable? table)
    {
        var zones = new List<(string Name, List<int> SpaceIds)>();
        if (table == null)
            return zones;
        var zoneName = table.RequiredColumn("zoneName");
        var spaceId = table.RequiredColumn("spaceId");
        var byName = new Dictionary<string, List<int>>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var name = TextCell(table, zoneName, r);
            if (!byName.TryGetValue(name, out var spaces))
            {
                spaces = [];
                byName.Add(name, spaces);
                zones.Add((name, spaces));
            }
            spaces.Add(checked((int)IntegerCell(table, spaceId, r)));
        }
        return zones;
    }

    /// <summary>Rows grouped by (target, psetName), groups and values in first-appearance order. The
    /// target column holds a STEP id for entity sets and a zone name for zone sets.</summary>
    private static IReadOnlyList<PsetGroup> GroupRows(IDataTable table, string targetColumn,
        Func<IDataTable, int, int, object> readKey)
    {
        var entityId = table.RequiredColumn(targetColumn);
        var psetName = table.RequiredColumn("psetName");
        var paramName = table.RequiredColumn("paramName");
        var paramValue = table.RequiredColumn("paramValue");
        var valueType = OptionalColumn(table, "valueType");
        var groups = new List<PsetGroup>();
        var byKey = new Dictionary<(object, string), PsetGroup>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var id = readKey(table, entityId, r);
            var pset = TextCell(table, psetName, r);
            var key = (id, pset);
            if (!byKey.TryGetValue(key, out var group))
            {
                group = new PsetGroup(id, pset, new List<IfcPropertyValue>());
                byKey.Add(key, group);
                groups.Add(group);
            }
            group.Values.Add(PsetValueTypes.Create(
                valueType < 0 ? null : table[valueType, r] as string,
                TextCell(table, paramName, r),
                TextCell(table, paramValue, r),
                r));
        }
        return groups;
    }

    private static int OptionalColumn(IDataTable table, string name)
    {
        for (var i = 0; i < table.Columns.Count; i++)
            if (table.Columns[i].Descriptor.Name == name)
                return i;
        return -1;
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
