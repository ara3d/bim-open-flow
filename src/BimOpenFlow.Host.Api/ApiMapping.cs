using System.Globalization;
using Ara3D.DataFlowEngine;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;
using ModelKind = BimOpenFlow.Contracts.ModelKind;
using NodeCapability = BimOpenFlow.Contracts.NodeCapability;
using NodeStatus = BimOpenFlow.Contracts.NodeStatus;
using ParamKind = BimOpenFlow.Contracts.ParamKind;
using PortType = BimOpenFlow.Contracts.PortType;

namespace BimOpenFlow.Host.Api;

/// <summary>Pure mapping from host/engine types to the generated contract types.
/// All enum crossings go by name, never by ordinal.</summary>
public static class ApiMapping
{
    public static TTo ByName<TTo>(this Enum value) where TTo : struct, Enum
        => Enum.Parse<TTo>(value.ToString());

    public static string ToUtcString(this DateTime utc)
        => utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static ModelSummary ToSummary(this ModelEntry entry)
        => new(entry.Id, entry.Name, entry.Kind.ByName<ModelKind>(),
            entry.SizeBytes, entry.LastWriteUtc.ToUtcString());

    public static EvalUpdate ToEvalUpdate(this EvalSnapshot snapshot, string analysisId)
        => new(analysisId, snapshot.Results.Values
            .OrderBy(r => r.NodeId, StringComparer.Ordinal)
            .Select(ToNodeState)
            .ToList());

    public static NodeState ToNodeState(this NodeResult result)
        => new(result.NodeId, result.Status.ByName<NodeStatus>(), result.Error, result.Warnings);

    public static NodeCatalog ToCatalog(this INodeRegistry registry)
        => new(registry.Nodes
            .Select(n => n.Spec.ToDescriptor())
            .OrderBy(d => d.Kind, StringComparer.Ordinal)
            .ThenBy(d => d.Version)
            .ToList());

    public static NodeDescriptor ToDescriptor(this NodeSpec spec)
        => new(spec.Kind, spec.Version, spec.Capability.ByName<NodeCapability>(),
            spec.Inputs.Select(ToDescriptor).ToList(),
            spec.Outputs.Select(ToDescriptor).ToList(),
            spec.Params.Select(ToDescriptor).ToList(),
            spec.Description);

    public static PortDescriptor ToDescriptor(this PortSpec port)
        => new(port.Name, port.Type.ByName<PortType>(), port.Optional);

    public static ParamDescriptor ToDescriptor(this ParamSpec param)
        => new(param.Name, param.Kind.ByName<ParamKind>(), param.Default, param.EnumValues);

    /// <summary>Pages a node output into a TableSlice; a non-table output becomes
    /// a one-row, one-column slice named after the port.</summary>
    public static TableSlice ToSlice(this FlowValue value, string port, int skip, int take)
        => value is TableValue table
            ? table.Table.ToSlice(skip, take)
            : ScalarSlice(value, port, skip, take);

    public static TableSlice ToSlice(this IDataTable table, int skip, int take)
    {
        var kinds = table.Columns.Select(c => ValueHash.ToColumnKind(c.Descriptor.Type)).ToList();
        var columns = table.Columns
            .Select((c, i) => new ColumnSchema(c.Descriptor.Name, kinds[i].ByName<ColumnType>()))
            .ToList();
        var total = table.Rows.Count;
        var start = Math.Clamp(skip, 0, total);
        var end = Math.Clamp(start + Math.Max(take, 0), start, total);
        var rows = new List<IReadOnlyList<object>>(end - start);
        for (var row = start; row < end; row++)
        {
            var cells = new object[columns.Count];
            for (var col = 0; col < columns.Count; col++)
                cells[col] = JsonSafeCell(kinds[col], table[col, row])!;
            rows.Add(cells);
        }
        return new(columns, rows, total, start);
    }

    private static TableSlice ScalarSlice(FlowValue value, string port, int skip, int take)
    {
        var column = new ColumnSchema(port, value.Kind.ByName<ColumnType>());
        var rows = skip == 0 && take > 0
            ? new[] { (IReadOnlyList<object>)new[] { JsonSafeValue(value)! } }
            : Array.Empty<IReadOnlyList<object>>();
        return new(new[] { column }, rows, 1, Math.Clamp(skip, 0, 1));
    }

    /// <summary>Scalar FlowValue to a JSON-native value (non-finite doubles as
    /// "NaN"/"Infinity"/"-Infinity", per the run-record convention).</summary>
    public static object? JsonSafeValue(FlowValue value)
        => value switch
        {
            BooleanValue b => b.Value,
            IntegerValue i => i.Value,
            NumberValue n => JsonSafeNumber(n.Value),
            TextValue t => t.Value,
            _ => throw new ArgumentException($"Value kind {value.Kind} has no scalar cell form"),
        };

    public static object? JsonSafeCell(ValueKind kind, object? cell)
        => cell is null or DBNull
            ? null
            : kind switch
            {
                ValueKind.Boolean => (bool)cell,
                ValueKind.Integer => Convert.ToInt64(cell, CultureInfo.InvariantCulture),
                ValueKind.Number => JsonSafeNumber(Convert.ToDouble(cell, CultureInfo.InvariantCulture)),
                ValueKind.Text => cell as string ?? cell.ToString()!,
                _ => throw new ArgumentException($"Cannot map table cell of kind {kind}"),
            };

    private static object JsonSafeNumber(double value)
        => double.IsNaN(value) ? "NaN"
            : double.IsPositiveInfinity(value) ? "Infinity"
            : double.IsNegativeInfinity(value) ? "-Infinity"
            : value;
}
