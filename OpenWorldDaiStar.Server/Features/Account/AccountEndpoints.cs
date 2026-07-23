using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Account;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Account/Register", RegisterAsync);
        endpoints.MapPost("/api/Account/Authenticate", AuthenticateAsync);
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        HttpRequest request,
        ApiRequestReader requestReader,
        ApiResponseWriter responseWriter,
        IdentityService identities,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await requestReader.ReadAsync<RegisterPayload>(request, cancellationToken);
            var account = await identities.RegisterAsync(payload.Name, cancellationToken);
            return responseWriter.Success(new AccountRegistResult
            {
                Token = account.LoginToken,
                ErrorType = AccountRegisterErrorTypes.None
            }, EndpointContract.Standard);
        }
        catch (ApiRequestException exception)
        {
            loggerFactory.CreateLogger("Account.Register")
                .LogWarning(exception, "Account.Register decode_failed=true");
            return Results.BadRequest();
        }
    }

    private static async Task<IResult> AuthenticateAsync(
        HttpRequest request,
        ApiRequestReader requestReader,
        ApiResponseWriter responseWriter,
        IdentityService identities,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await requestReader.ReadAsync<AuthenticatePayload>(request, cancellationToken);
            var session = await identities.AuthenticateAsync(payload.LoginToken, cancellationToken);
            if (session is null)
                return Results.Unauthorized();

            return responseWriter.Success(new AuthenticateResult
            {
                Token = session.AccessToken,
                BanLevel = BanLevels.Normal,
                WarnedUntil = null
            }, EndpointContract.Standard);
        }
        catch (ApiRequestException exception)
        {
            loggerFactory.CreateLogger("Account.Authenticate")
                .LogWarning(exception, "Account.Authenticate decode_failed=true");
            return Results.BadRequest();
        }
    }
}
