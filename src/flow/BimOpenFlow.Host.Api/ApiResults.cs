using Ara3D.DataFlowEngine;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>Contract-shaped results and the one exception-to-status translation
/// used by every handler: not-found IO errors become 404 ApiError, invalid
/// input becomes 400 ApiError.</summary>
internal static class ApiResults
{
    public static IResult Json<T>(T value)
        => Results.Json(value, ApiJson.Options);

    public static IResult NotFound(string message)
        => Results.Json(new ApiError(message), ApiJson.Options, statusCode: StatusCodes.Status404NotFound);

    public static IResult BadRequest(string message)
        => Results.Json(new ApiError(message), ApiJson.Options, statusCode: StatusCodes.Status400BadRequest);

    public static IResult Conflict(string message)
        => Results.Json(new ApiError(message), ApiJson.Options, statusCode: StatusCodes.Status409Conflict);

    public static IResult Guard(Func<IResult> handler)
    {
        try
        {
            return handler();
        }
        catch (InvalidGraphException e)
        {
            return BadRequest(e.Message);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or System.Text.Json.JsonException)
        {
            return BadRequest(e.Message);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return NotFound(e.Message);
        }
    }
}
