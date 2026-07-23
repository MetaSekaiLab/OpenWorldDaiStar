using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;
using SiriusApi.Shared.Models.Player;

namespace OpenWorldDaiStar.Features.Player;

public static class PlayerEndpoints
{
    private static readonly EndpointContract SplashLastDisplayContract = new(
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Lz4BlockArray,
        FrameCompression.Standard,
        FrameCompression.Standard);

    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Player/UpdateGameHintRead", UpdateGameHintReadAsync);
        endpoints.MapPost(
            "/api/Player/UpdateSplashLastDisplayTime",
            UpdateSplashLastDisplayTimeAsync);
        return endpoints;
    }

    private static async Task<IResult> UpdateSplashLastDisplayTimeAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        var updatedUser = new User
        {
            Id = session.Account.Id,
            SplashLastDisplayedAt = YmstDateTime.FromUnixMillisecondsAsJstWire(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };

        loggerFactory.CreateLogger("Player.UpdateSplashLastDisplayTime").LogInformation(
            "Player.UpdateSplashLastDisplayTime account_id={AccountId} " +
            "displayed_at={DisplayedAt} success=true",
            session.Account.Id,
            updatedUser.SplashLastDisplayedAt);
        return responseWriter.Success(
            new BooleanResult { IsSuccess = true },
            [updatedUser],
            SplashLastDisplayContract);
    }

    private static async Task<IResult> UpdateGameHintReadAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiRequestReader requestReader,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        var logger = loggerFactory.CreateLogger("Player.UpdateGameHintRead");
        try
        {
            var payload = await requestReader.ReadAsync<UpdateGameHintPayload>(
                request,
                cancellationToken);
            if (payload.Categories is not { Length: > 0 } ||
                payload.Categories.Any(category => !Enum.IsDefined(category)))
            {
                logger.LogWarning(
                    "Player.UpdateGameHintRead account_id={AccountId} invalid_categories=true success=false",
                    session.Account.Id);
                return Results.BadRequest();
            }

            var categories = payload.Categories
                .Distinct()
                .OrderBy(category => (int)category)
                .ToArray();
            logger.LogInformation(
                "Player.UpdateGameHintRead account_id={AccountId} categories={Categories} " +
                "already_read=true success=true",
                session.Account.Id,
                string.Join(",", categories.Select(category => (int)category)));
            return responseWriter.Success(
                new BooleanResult { IsSuccess = true },
                EndpointContract.Standard);
        }
        catch (ApiRequestException exception)
        {
            logger.LogWarning(
                exception,
                "Player.UpdateGameHintRead account_id={AccountId} decode_failed=true success=false",
                session.Account.Id);
            return Results.BadRequest();
        }
    }
}
