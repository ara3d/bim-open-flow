using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Catalog;

namespace BimOpenFlow.Host.Api;

/// <summary>GET /api/models/{id}/entities/{localId}/properties — one entity's
/// parameters, grouped and resolved to text, from the catalog's cached index.</summary>
internal static class EntityPropertiesEndpoint
{
    public static void MapEntityProperties(this IEndpointRouteBuilder app, ModelCatalog catalog)
        => app.MapGet(ApiRoutes.GetEntityProperties, (string id, long localId) =>
            ApiResults.Guard(() => GetProperties(catalog, id, localId)));

    private static IResult GetProperties(ModelCatalog catalog, string id, long localId)
    {
        var entry = catalog.Scan().FirstOrDefault(e => e.Id == id);
        if (entry is null)
            return ApiResults.NotFound($"Model '{id}' not found");
        var entity = catalog.GetEntityIndex(entry).Find(localId);
        return entity is null
            ? ApiResults.NotFound($"Entity {localId} not found in model '{id}'")
            : ApiResults.Json(entity.ToProperties());
    }
}
