using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host;

// TODO: promote this route to contracts/contracts.json ("endpoints") as a binary
// model-bytes endpoint and move the handler into BimOpenFlow.Host.Api; the
// contract has no binary endpoint shape yet, so it lives here for now.
/// <summary>GET /api/models/{id}/bos — the BOS form of a model as raw bytes
/// (converting IFC sources through the catalog cache on first request).</summary>
public static class ModelBytesEndpoint
{
    public const string Route = "/api/models/{id}/bos";

    public static IEndpointRouteBuilder MapModelBytes(this IEndpointRouteBuilder app, ModelCatalog catalog)
    {
        app.MapGet(Route, (string id) => GetBos(catalog, id));
        return app;
    }

    private static IResult GetBos(ModelCatalog catalog, string id)
    {
        var entry = catalog.Scan().FirstOrDefault(e => e.Id == id);
        return entry is null
            ? Results.Json(new ApiError($"Model '{id}' not found"),
                statusCode: StatusCodes.Status404NotFound)
            : Results.File(catalog.GetBos(entry), "application/octet-stream");
    }
}
