using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace BimOpenFlow.Host;

/// <summary>Serves a folder of built web pages at the host's root, beside /api, so a release
/// needs no second web server. A request for a folder answers its index.html, or else its
/// index.json (the notebook pages list their sample notebooks that way).</summary>
public static class StaticWeb
{
    private static readonly IReadOnlyDictionary<string, string> ExtraTypes = new Dictionary<string, string>
    {
        [".md"] = "text/markdown; charset=utf-8",
        [".bos"] = "application/octet-stream",
        [".glb"] = "model/gltf-binary",
    };

    /// <summary>Adds the file server for <paramref name="dir"/> to the pipeline; false when the folder is missing.</summary>
    public static bool Use(WebApplication app, string dir)
    {
        if (!Directory.Exists(dir))
            return false;
        var files = new PhysicalFileProvider(Path.GetFullPath(dir));
        var types = new FileExtensionContentTypeProvider();
        foreach (var (extension, type) in ExtraTypes)
            types.Mappings[extension] = type;
        var defaults = new DefaultFilesOptions { FileProvider = files };
        defaults.DefaultFileNames.Clear();
        defaults.DefaultFileNames.Add("index.html");
        defaults.DefaultFileNames.Add("index.json");
        app.UseDefaultFiles(defaults);
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ContentTypeProvider = types });
        return true;
    }
}
