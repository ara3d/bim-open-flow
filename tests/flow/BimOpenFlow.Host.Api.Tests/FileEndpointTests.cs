using static BimOpenFlow.Host.Api.Tests.TestGraphs;

namespace BimOpenFlow.Host.Api.Tests;

[TestFixture]
public sealed class FileEndpointTests
{
    private static string Slash(string path) => path.Replace('\\', '/');

    private static string ModelsDirFull => Path.GetFullPath(ApiTestServer.ModelsDir);

    [Test]
    public async Task BrowseFolder_WithoutDir_ListsTheRoots()
    {
        using var listing = await GetJson("/api/files");
        var root = listing.RootElement;
        Assert.That(root.GetProperty("dir").GetString(), Is.EqualTo(""));
        var roots = root.GetProperty("roots").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.That(roots, Is.EqualTo(new[] { Slash(ModelsDirFull) }));
        Assert.That(root.GetProperty("entries").EnumerateArray().Single().GetProperty("path").GetString(),
            Is.EqualTo(roots[0]));
    }

    [Test]
    public async Task BrowseFolder_InsideARoot_ListsMatchingFilesOnly()
    {
        using var listing = await GetJson(
            $"/api/files?dir={Uri.EscapeDataString(ModelsDirFull)}&ext={Uri.EscapeDataString(".bos")}");
        var names = listing.RootElement.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()).ToList();
        Assert.That(names, Is.EqualTo(new[] { "sample model.bos", "walls.bos" }));
        Assert.That(listing.RootElement.GetProperty("parent").GetString(), Is.EqualTo(""));
    }

    [Test]
    public async Task BrowseFolder_OutsideTheRoots_IsRefused()
    {
        var response = await ApiTestServer.Client.GetAsync(
            $"/api/files?dir={Uri.EscapeDataString(Path.GetTempPath())}");
        Assert.That((int)response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task StatPath_ReportsExistenceAndRelativePath()
    {
        var present = Path.Combine(ModelsDirFull, "walls.bos");
        using var found = await GetJson($"/api/files/stat?path={Uri.EscapeDataString(present)}");
        Assert.That(found.RootElement.GetProperty("exists").GetBoolean(), Is.True);
        Assert.That(found.RootElement.GetProperty("relative").GetString(), Is.EqualTo("walls.bos"));

        using var missing = await GetJson(
            $"/api/files/stat?path={Uri.EscapeDataString(Path.Combine(ModelsDirFull, "nope.bos"))}");
        Assert.That(missing.RootElement.GetProperty("exists").GetBoolean(), Is.False);
        Assert.That(missing.RootElement.GetProperty("relative").GetString(), Is.EqualTo("nope.bos"));
    }
}
