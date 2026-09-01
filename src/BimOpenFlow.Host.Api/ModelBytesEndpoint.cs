using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Api;

/// <summary>GET /api/models/{id}/bos — the BOS form of a model as raw bytes
/// (converting IFC sources through the catalog cache on first request).</summary>
internal static class ModelBytesEndpoint
{
    public static void MapModelBytes(this IEndpointRouteBuilder app, ModelCatalog catalog)
        => app.MapGet(ApiRoutes.GetModelBos, (string id) =>
            ApiResults.Guard(() => GetBos(catalog, id)));

    private static IResult GetBos(ModelCatalog catalog, string id)
    {
        var entry = catalog.Scan().FirstOrDefault(e => e.Id == id);
        return entry is null
            ? ApiResults.NotFound($"Model '{id}' not found")
            : Results.File(catalog.GetBos(entry), "application/octet-stream");
    }
}
