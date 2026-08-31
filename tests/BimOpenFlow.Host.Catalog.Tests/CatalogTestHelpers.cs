using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Catalog.Tests;

/// <summary>Counts conversions and writes the source bytes to the target,
/// so cache-hit logic is testable without real IFC files.</summary>
internal sealed class StubConverter : IIfcConverter
{
    public int Calls;

    public void Convert(string ifcPath, string bosPath)
    {
        Calls++;
        File.Copy(ifcPath, bosPath, overwrite: true);
    }
}

internal static class CatalogTestHelpers
{
    public static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bimopenflow-catalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void DeleteTempDir(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    public static string WriteFile(string dir, string relativePath, string content = "fake")
    {
        var path = Path.Combine(dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Walks up from the test directory looking for data/{fileName};
    /// ignores the test when absent (test data is never committed).</summary>
    public static string FindData(string fileName)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data", fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        Assert.Ignore($"Sample model {fileName} not found under a 'data' folder.");
        return "";
    }
}
