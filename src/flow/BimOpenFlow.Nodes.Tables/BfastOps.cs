using System.Runtime.CompilerServices;
using Ara3D.IO.BFAST;
using Ara3D.Memory;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads a BFAST container through a memory-mapped view: the buffer directory
/// without touching buffer bytes, and one buffer reinterpreted as a primitive element type.
/// BFAST stores no element types, so the caller names one.</summary>
internal static class BfastOps
{
    public sealed record BufferRange(string Name, long Begin, long ByteLength);

    /// <summary>The element types bfast.buffer accepts; integers widen to long, floats to double.</summary>
    public static readonly IReadOnlyList<string> ElementTypes = ["uint8", "int16", "int32", "int64", "float32", "float64"];

    public static Type ColumnType(string elementType)
        => elementType is "float32" or "float64" ? typeof(double) : typeof(long);

    /// <summary>Every buffer's name, absolute byte offset, and length, in file order.</summary>
    public static IReadOnlyList<BufferRange> Ranges(string path)
        => WithReader(path, Ranges);

    private static IReadOnlyList<BufferRange> Ranges(BFastReader reader)
        => reader.BufferNames.Select((name, i) => new BufferRange(name, reader.Ranges[i + 1].Begin, reader.Ranges[i + 1].Count)).ToList();

    /// <summary>The named buffer's values as boxed long or double cells.</summary>
    public static object?[] ReadValues(string path, string name, string elementType, string kind)
        => WithReader(path, reader =>
        {
            var ranges = Ranges(reader);
            var range = ranges.FirstOrDefault(r => r.Name == name)
                ?? throw new ArgumentException(
                    $"{kind}: no buffer named '{name}' in {path}; buffers: {string.Join(", ", ranges.Select(r => r.Name))}.");
            return elementType switch
            {
                "uint8" => Read<byte>(reader.View, range, kind, v => (long)v),
                "int16" => Read<short>(reader.View, range, kind, v => (long)v),
                "int32" => Read<int>(reader.View, range, kind, v => (long)v),
                "int64" => Read<long>(reader.View, range, kind, v => v),
                "float32" => Read<float>(reader.View, range, kind, v => (double)v),
                "float64" => Read<double>(reader.View, range, kind, v => v),
                _ => throw new ArgumentException($"{kind}: unknown element type '{elementType}'; one of {string.Join(", ", ElementTypes)}."),
            };
        });

    private static object?[] Read<T>(MemoryMappedView view, BufferRange range, string kind, Func<T, object> widen)
        where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        if (range.ByteLength % size != 0)
            throw new ArgumentException(
                $"{kind}: buffer '{range.Name}' holds {range.ByteLength} bytes, not a multiple of the {size}-byte element type.");
        var count = checked((int)(range.ByteLength / size));
        var raw = new T[count];
        view.Accessor.ReadArray(range.Begin, raw, 0, count);
        var cells = new object?[count];
        for (var i = 0; i < count; i++)
            cells[i] = widen(raw[i]);
        return cells;
    }

    private static TResult WithReader<TResult>(string path, Func<BFastReader, TResult> read)
    {
        TResult result = default!;
        MemoryMappedView.ReadFile(path, view => result = read(new BFastReader(view)));
        return result;
    }
}
