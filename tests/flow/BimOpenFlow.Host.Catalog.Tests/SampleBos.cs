using Ara3D.BimOpenSchema;
using Ara3D.BimOpenSchema.IO;
using Ara3D.Utils;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>A synthetic BOS model for tests that read a real archive through the catalog
/// (here, and in BimOpenFlow.Host.Api.Tests, which compiles this file too):
/// one document, a wall category, and <see cref="WallCount"/> walls, each with a carbon
/// figure, an external flag, a height, and a mark. Built in code, so the repository ships
/// no building model whose licence is in question (TKT-144).</summary>
public static class SampleBos
{
    public const string WallCategory = "IFCWALLSTANDARDCASE";
    public const string CarbonGroup = "Pset_OperationalCarbon";
    public const string CarbonName = "OperationalCarbon_kgCO2e_per_year";
    public const int WallCount = 120;

    public static BimData Build()
    {
        var b = new BimDataBuilder();
        var doc = b.AddDocument("Sample walls", "sample://walls");
        var category = b.AddEntity(1, "GUID-CATEGORY", doc, WallCategory,
            BimDataBuilder.InvalidEntityIndex, BimDataBuilder.InvalidEntityIndex);
        for (var i = 0; i < WallCount; i++)
        {
            var wall = b.AddEntity(100 + i, $"GUID-WALL-{i:D3}", doc, $"Wall {i:D3}",
                category, BimDataBuilder.InvalidEntityIndex);
            b.AddParameter(wall, 10.0 + i, CarbonName, "kgCO2e", CarbonGroup);
            b.AddParameter(wall, i % 2, "IsExternal", "", "Pset_WallCommon");
            b.AddParameter(wall, 3.0, "Height", "m", "Dimensions");
            b.AddParameter(wall, $"W-{i:D3}", "Mark", "", "Identity");
        }
        return b.Build();
    }

    /// <summary>Writes <see cref="Build"/> as a BOS archive at <paramref name="path"/> and returns the path.</summary>
    public static string Write(string path)
    {
        Build().WriteToParquetZip(new FilePath(path));
        return path;
    }
}
