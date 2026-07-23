using Microsoft.Extensions.Options;
using OpenWorldDaiStar.App;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.MasterData;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/data/master", HandleAsync);
        return endpoints;
    }

    private static IResult HandleAsync(
        IOptions<GameOptions> options,
        ApiResponseWriter responses)
    {
        var game = options.Value;
        var result = new MasterDataManifest
        {
            Uri = game.MasterDataUri,
            SasToken = string.Empty,
            Version = game.MasterDataVersion,
            PublishTimestamp = game.MasterDataPublishTimestamp
        };
        return responses.Success(result, EndpointContract.Standard);
    }
}
