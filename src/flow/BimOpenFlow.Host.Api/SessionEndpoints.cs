using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>The editor session: what the studio has open and selected.</summary>
internal static class SessionEndpoints
{
    /// <summary>GET and PUT ApiRoutes.GetSession/PutSession; the PUT body is parsed with ApiJson.Options, and malformed JSON or a bad id returns a 400 ApiError.</summary>
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app, EditorSessions editor)
    {
        app.MapGet(ApiRoutes.GetSession, () => ApiResults.Guard(() =>
            ApiResults.Json(editor.Read())));

        app.MapPut(ApiRoutes.PutSession, async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var text = await reader.ReadToEndAsync(context.RequestAborted);
            return ApiResults.Guard(() =>
            {
                var session = System.Text.Json.JsonSerializer.Deserialize<EditorSession>(text, ApiJson.Options)
                    ?? throw new System.Text.Json.JsonException("Empty request body");
                return ApiResults.Json(editor.Write(session, DateTimeOffset.UtcNow));
            });
        });
    }
}
