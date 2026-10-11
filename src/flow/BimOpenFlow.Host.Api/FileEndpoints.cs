using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Api;

/// <summary>
/// GET /api/files (a folder of a model root, for the FilePath control's browse button) and
/// GET /api/files/stat (whether a path exists and which root it sits under). Browsing is limited to
/// the roots; the stat answers for any path, because a run reads the path as written.
/// </summary>
internal static class FileEndpoints
{
    public static void MapFileEndpoints(this IEndpointRouteBuilder app, ModelRootFiles files)
    {
        app.MapGet(ApiRoutes.BrowseFolder, (string? dir, string? ext) =>
            ApiResults.Guard(() => ApiResults.Json(Browse(files, dir ?? "", ext ?? ""))));
        app.MapGet(ApiRoutes.StatPath, (string path) =>
            ApiResults.Guard(() => ApiResults.Json(Stat(files, path))));
    }

    private static FolderListing Browse(ModelRootFiles files, string dir, string ext)
    {
        var folder = files.Browse(dir, ext);
        return new(folder.Dir, folder.Parent, files.Roots,
            folder.Entries.Select(e => new FolderEntry(e.Name, e.Path, e.IsDirectory, e.SizeBytes)).ToList());
    }

    private static PathStatus Stat(ModelRootFiles files, string path)
    {
        var status = files.Stat(path);
        return new(status.Path, status.Exists, status.Root, status.Relative);
    }
}
