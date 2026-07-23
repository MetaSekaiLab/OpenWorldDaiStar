using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Login;

public static class LoginEndpoints
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Login", LoginAsync);
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpRequest request,
        ApiRequestReader requestReader,
        ApiResponseWriter responseWriter,
        SessionAuthenticator authenticator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            _ = await requestReader.ReadAsync<LoginPayload>(request, cancellationToken);
        }
        catch (ApiRequestException exception)
        {
            loggerFactory.CreateLogger("Login")
                .LogWarning(exception, "Login decode_failed=true");
            return Results.BadRequest();
        }

        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Login").LogInformation(
            "Login account_id={AccountId} session_key={SessionKey} success=true",
            session.Account.Id, session.SessionKey);
        return responseWriter.Success(new LoginResult
        {
            InvalidedStarPasses = [],
            LoginPassNotification = LoginPassNotificationTypes.None,
            IsApproachingLoginPassInvalided = false,
            InvalidedItemMasterIds = [],
            ApproachingItemMasterIds = [],
            StoryEventPointExchangeResult = [],
            InvalidedBuffItemMasterIds = []
        }, EndpointContract.Standard);
    }
}
