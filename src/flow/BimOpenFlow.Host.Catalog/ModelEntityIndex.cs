using System.Globalization;
using Ara3D.BimOpenSchema;

namespace BimOpenFlow.Host.Catalog;

/// <summary>Every entity of one BOS model resolved to displayable text and keyed
/// by LocalId, built once per model so a lookup is a dictionary hit. Where two
/// documents share a LocalId the first entity in table order wins.</summary>
public sealed class ModelEntityIndex
{
    private readonly Dictionary<long, ModelEntity> _byLocalId;

    private ModelEntityIndex(Dictionary<long, ModelEntity> byLocalId)
        => _byLocalId = byLocalId;

    public int Count => _byLocalId.Count;

    public IReadOnlyCollection<ModelEntity> All => _byLocalId.Values;

    public ModelEntity? Find(long localId)
        => _byLocalId.TryGetValue(localId, out var entity) ? entity : null;

    public static ModelEntityIndex Build(IBimData data)
    {
        var grouped = GroupParameters(data);
        var byLocalId = new Dictionary<long, ModelEntity>(data.Entities.Length);
        for (var i = 0; i < data.Entities.Length; i++)
        {
            var entity = data.Entities[i];
            if (byLocalId.ContainsKey(entity.LocalId))
                continue;
            byLocalId.Add(entity.LocalId, new(
                entity.LocalId,
                Text(data, entity.GlobalId),
                Text(data, entity.Name),
                CategoryName(data, entity.Category),
                Sorted(grouped[i])));
        }
        return new(byLocalId);
    }

    private static List<ModelEntityParameter>[] GroupParameters(IBimData data)
    {
        var grouped = new List<ModelEntityParameter>[data.Entities.Length];
        for (var i = 0; i < grouped.Length; i++)
            grouped[i] = new();
        foreach (var p in data.Parameters)
        {
            var entity = (int)p.Entity;
            if (entity < 0 || entity >= grouped.Length)
                continue;
            if (Resolve(data, p) is { } resolved)
                grouped[entity].Add(resolved);
        }
        return grouped;
    }

    private static IReadOnlyList<ModelEntityParameter> Sorted(List<ModelEntityParameter> parameters)
    {
        // Case-insensitive so Pset_* and PSET_* sets interleave by name rather than by case.
        parameters.Sort((a, b) =>
        {
            var byGroup = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
            return byGroup != 0 ? byGroup : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return parameters;
    }

    private static ModelEntityParameter? Resolve(IBimData data, Parameter p)
    {
        var index = (int)p.Descriptor;
        if (index < 0 || index >= data.Descriptors.Length)
            return null;
        var descriptor = data.Descriptors[index];
        return new(
            Text(data, descriptor.Group) ?? "",
            Text(data, descriptor.Name) ?? "",
            ValueText(data, descriptor.Type, p.Value),
            Text(data, descriptor.Units));
    }

    private static string ValueText(IBimData data, ParameterType type, int value)
        => type switch
        {
            ParameterType.String => Text(data, (StringIndex)value) ?? "",
            ParameterType.Number => Number(data, value),
            ParameterType.Entity => EntityName(data, (EntityIndex)value),
            ParameterType.Point => PointText(data, value),
            _ => value.ToString(CultureInfo.InvariantCulture),
        };

    private static string Number(IBimData data, int value)
        => value >= 0 && value < data.Numbers.Length
            ? data.Numbers[value].ToString("R", CultureInfo.InvariantCulture)
            : "";

    private static string PointText(IBimData data, int value)
    {
        if (value < 0 || value >= data.Points.Length)
            return "";
        var p = data.Points[value];
        return FormattableString.Invariant($"{p.X}, {p.Y}, {p.Z}");
    }

    private static string EntityName(IBimData data, EntityIndex index)
        => (int)index >= 0 && (int)index < data.Entities.Length
            ? Text(data, data.Entities[(int)index].Name) ?? ""
            : "";

    private static string? CategoryName(IBimData data, EntityIndex index)
        => (int)index >= 0 && (int)index < data.Entities.Length
            ? Text(data, data.Entities[(int)index].Name)
            : null;

    /// <summary>An interned string, or null when the index is out of range or the
    /// string is empty — both mean "the model does not say".</summary>
    private static string? Text(IBimData data, StringIndex index)
    {
        var i = (int)index;
        if (i < 0 || i >= data.Strings.Length)
            return null;
        var text = data.Strings[i];
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
