using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Friends;

public static class FriendsEndpoints
{
    public static IEndpointRouteBuilder MapFriendsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ReceivedRequest", GetReceivedRequestAsync);
        return endpoints;
    }

    private static async Task<IResult> GetReceivedRequestAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Friends.ReceivedRequest").LogInformation(
            "Friends.ReceivedRequest account_id={AccountId} result_count=0 current_friend_count=0 success=true",
            session.Account.Id);
        return responseWriter.Success(
            new FriendListResult
            {
                Results = [],
                CurrentFriendCount = 0
            },
            EndpointContract.Standard);
    }
}
