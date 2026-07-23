using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.FriendInvitation;

public static class FriendInvitationEndpoints
{
    public static IEndpointRouteBuilder MapFriendInvitationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/FriendInvitation/Update", UpdateAsync);
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("FriendInvitation.Update").LogInformation(
            "FriendInvitation.Update account_id={AccountId} success=true",
            session.Account.Id);
        return responseWriter.Success(
            new BooleanResult { IsSuccess = true },
            EndpointContract.Standard);
    }
}
