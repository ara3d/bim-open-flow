using System.Globalization;
using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;

namespace BimOpenFlow.Host.Catalog;

/// <summary>Every entity of one BOS model resolved to displayable text, built once per
/// model so a lookup is a dictionary hit. <see cref="Find"/> is keyed by LocalId: an
/// entity without one (-1, as every category entity has) is in <see cref="All"/> but not
/// found by id, and where two documents share a LocalId the first entity in table order
/// wins. An absent string stays null; a parameter whose value is absent is left out.</summary>
public sealed class ModelEntityIndex
{
    /// <summary>The tables <see cref="Build"/> uses: entities and their parameters (a read adds the
    /// descriptors, strings, numbers, and points those index). Geometry, relations, documents, and
    /// diagnostics are never read.</summary>
    public const BosTables TablesRead = BosTables.Entities | BosTables.Parameters;

    private const long NoLocalId = -1;

    private readonly IReadOnlyList<ModelEntity> _all;
    private readonly Dictionary<long, ModelEntity> _byLocalId;

    private ModelEntityIndex(IReadOnlyList<ModelEntity> all, Dictionary<long, ModelEntity> byLocalId)
        => (_all, _byLocalId) = (all, byLocalId);

    /// <summary>Every entity in the model, including those without a LocalId.</summary>
    public int Count => _all.Count;

    /// <summary>Every entity in the model, in table order.</summary>
    public IReadOnlyList<ModelEntity> All => _all;

    /// <summary>The first entity with this LocalId, or null; never an entity without one.</summary>
    public ModelEntity? Find(long localId)
        => _byLocalId.TryGetValue(localId, out var entity) ? entity : null;

    public static ModelEntityIndex Build(IBimData data)
    {
        var grouped = GroupParameters(data);
        var all = new ModelEntity[data.Entities.Length];
        var byLocalId = new Dictionary<long, ModelEntity>(data.Entities.Length);
        for (var i = 0; i < data.Entities.Length; i++)
        {
            var entity = data.Entities[i];
            all[i] = new(
                entity.LocalId,
                data.Label(entity.GlobalId),
                data.Label(entity.Name),
                data.GetCategoryName(entity),
                Sorted(grouped[i]));
            if (entity.LocalId != NoLocalId)
                byLocalId.TryAdd(entity.LocalId, all[i]);
        }
        return new(all, byLocalId);
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
        // A null group or name sorts first.
        parameters.Sort((a, b) =>
        {
            var byGroup = string.Compare(a.Group, b.Group, StringComparison.OrdinalIgnoreCase);
            return byGroup != 0 ? byGroup : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return parameters;
    }

    /// <summary>The parameter as text, or null when its descriptor or its value is absent.</summary>
    private static ModelEntityParameter? Resolve(IBimData data, Parameter p)
    {
        if (data.Get(p.Descriptor) is not { } descriptor)
            return null;
        if (!TryValueText(data, descriptor.Type, p.Value, out var value))
            return null;
        return new(data.Label(descriptor.Group), data.Label(descriptor.Name), value, data.Label(descriptor.Units));
    }

    /// <summary>False when the value index is absent (-1) or out of range. An Int value is
    /// the value itself, never an index. An entity reference shows the entity's name, null
    /// when that entity has none.</summary>
    private static bool TryValueText(IBimData data, ParameterType type, int value, out string? text)
    {
        switch (type)
        {
            case ParameterType.String:
                text = data.Get((StringIndex)value);
                return text != null;
            case ParameterType.Number:
                var number = data.Get((NumberIndex)value);
                text = number?.ToString("R", CultureInfo.InvariantCulture);
                return number != null;
            case ParameterType.Point:
                var point = data.Get((PointIndex)value);
                text = point is { } pt ? FormattableString.Invariant($"{pt.X}, {pt.Y}, {pt.Z}") : null;
                return point != null;
            case ParameterType.Entity:
                var target = data.Get((EntityIndex)value);
                text = data.GetEntityName(target);
                return target != null;
            default:
                text = value.ToString(CultureInfo.InvariantCulture);
                return true;
        }
    }
}
