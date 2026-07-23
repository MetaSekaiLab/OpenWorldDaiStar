using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.UserData;

public static class UserDataEndpoints
{
    private static readonly EndpointContract UserDataContract = new(
        FrameCompression.Standard,
        FrameCompression.Lz4BlockArray,
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Standard);

    public static IEndpointRouteBuilder MapUserDataEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/data/user", GetUserDataAsync);
        return endpoints;
    }

    private static async Task<IResult> GetUserDataAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        IDataObject[] objects = [];
        loggerFactory.CreateLogger("Data.User").LogInformation(
            "Data.User account_id={AccountId} objects={ObjectCount} success=true",
            session.Account.Id, objects.Length);
        return responseWriter.Success(objects, UserDataContract);
    }
}
