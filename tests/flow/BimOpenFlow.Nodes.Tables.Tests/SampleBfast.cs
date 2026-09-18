using System.Text;
using Ara3D.IO.BFAST;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>The committed samples/tables/sample.bfast, defined here and written with the SDK
/// writer: eight order ids and quantities (int32), eight unit prices (float64), and the product
/// codes as null-terminated ASCII (25 bytes: an untyped buffer no wider element type divides).
/// Regenerate with the Explicit test.</summary>
public static class SampleBfast
{
    public static readonly int[] OrderIds = [1, 2, 3, 4, 5, 6, 7, 8];
    public static readonly int[] Quantities = [2, 1, 5, 3, 1, 4, 2, 6];
    public static readonly double[] UnitPrices = [12.5, 30, 4.25, 99.99, 12.5, 4.25, 30, 7.75];
    public static readonly string[] ProductCodes = ["P1", "P2", "P3", "P4", "P1", "P3", "P2", "P10"];

    public static IReadOnlyList<(string Name, byte[] Bytes)> Buffers =>
    [
        ("orderIds", Bytes(OrderIds)),
        ("quantities", Bytes(Quantities)),
        ("unitPrices", Bytes(UnitPrices)),
        ("productCodes", Encoding.ASCII.GetBytes(string.Concat(ProductCodes.Select(c => c + '\0')))),
    ];

    public static string CommittedPath => RepoPaths.Samples("tables", "sample.bfast");

    /// <summary>Writes the buffers as a BFAST file, replacing any existing file (the SDK writer
    /// opens without truncating).</summary>
    public static void Write(string path, IReadOnlyList<(string Name, byte[] Bytes)> buffers)
    {
        File.Delete(path);
        BFast.Write(path, buffers.Select(b => b.Name), buffers.Select(b => (long)b.Bytes.Length),
            (stream, index, _, _) =>
            {
                stream.Write(buffers[index].Bytes);
                return buffers[index].Bytes.Length;
            });
    }

    public static void Write(string path) => Write(path, Buffers);

    private static byte[] Bytes<T>(T[] values) where T : unmanaged
        => System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
}

/// <summary>Keeps samples/tables/sample.bfast equal to SampleBfast, and regenerates it on request.</summary>
[TestFixture]
public sealed class SampleBfastFixtureTests
{
    /// <summary>Reads through the memory-mapped BFastReader; the SDK's stream reader
    /// (BFast.Read) adds DataStart to offsets that are already absolute and fails on its own output.</summary>
    private static IReadOnlyList<(string Name, byte[] Bytes)> ReadAll(string path)
    {
        var buffers = new List<(string, byte[])>();
        BFastReader.Read(path, (name, view, _) =>
        {
            var bytes = new byte[view.Size];
            view.Accessor.ReadArray(0, bytes, 0, bytes.Length);
            buffers.Add((name, bytes));
        });
        return buffers;
    }

    [Test]
    public void CommittedFile_MatchesTheDefinition()
        => Assert.That(ReadAll(SampleBfast.CommittedPath), Is.EqualTo(SampleBfast.Buffers));

    [Test]
    public void CommittedFile_StaysSmall()
        => Assert.That(new FileInfo(SampleBfast.CommittedPath).Length, Is.LessThan(10 * 1024));

    [Test, Explicit("Rewrites samples/tables/sample.bfast from SampleBfast; run after changing the definition.")]
    public void Regenerate()
        => SampleBfast.Write(SampleBfast.CommittedPath);
}
