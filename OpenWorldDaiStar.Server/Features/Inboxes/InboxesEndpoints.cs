using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Inboxes;

public static class InboxesEndpoints
{
    public static IEndpointRouteBuilder MapInboxesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Inboxes/CheckPackagesAsync", CheckPackagesAsync);
        return endpoints;
    }

    private static async Task<IResult> CheckPackagesAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Inboxes.CheckPackages").LogInformation(
            "Inboxes.CheckPackages account_id={AccountId} has_pending_packages=false success=true",
            session.Account.Id);
        return responseWriter.Success(
            new BooleanResult { IsSuccess = false },
            EndpointContract.Standard);
    }
}
