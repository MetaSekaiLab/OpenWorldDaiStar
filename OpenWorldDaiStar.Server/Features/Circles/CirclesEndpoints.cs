using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Circles;

public static class CirclesEndpoints
{
    private static readonly CircleInformationResult[] EmptyInvitations = [];

    public static IEndpointRouteBuilder MapCirclesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/Circles/Invited", GetInvitedCirclesAsync);
        return endpoints;
    }

    private static async Task<IResult> GetInvitedCirclesAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Circles.Invited").LogInformation(
            "Circles.Invited account_id={AccountId} result_count=0 success=true",
            session.Account.Id);
        return responseWriter.Success(EmptyInvitations, EndpointContract.Standard);
    }
}
