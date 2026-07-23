using Microsoft.Extensions.Options;
using OpenWorldDaiStar.App;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Environment;

public static class EnvironmentEndpoints
{
    public static IEndpointRouteBuilder MapEnvironmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Environment", HandleAsync);
        return endpoints;
    }

    private static IResult HandleAsync(
        IOptions<GameOptions> options,
        ApiResponseWriter responses)
    {
        var game = options.Value;
        var result = new EnvironmentResult
        {
            ApplicationVersion = game.ApplicationVersion,
            AssetVersion = game.AssetVersion,
            ApiEndpoint = game.ApiBaseUrl.TrimEnd('/'),
            MaintenanceApiEndpoint = game.MaintenanceApiEndpoint.TrimEnd('/'),
            NewsApiEndpoint = game.NewsApiEndpoint.TrimEnd('/'),
            IsMaintenance = false,
            MasterDataUrl = game.MasterDataUrl.TrimEnd('/'),
            StaticContentUrl = game.StaticContentUrl.TrimEnd('/'),
            AssetUrl = game.AssetBaseUrl.TrimEnd('/'),
            IsAppReview = false,
            PhotoContentUrl = game.PhotoContentUrl.TrimEnd('/'),
            MultiRealTimeServerUrl = game.MultiRealTimeServerUrl.TrimEnd('/'),
            ExternalPaymentUrl = game.ExternalPaymentUrl.TrimEnd('/')
        };
        return responses.Success(result, EndpointContract.Standard);
    }
}
