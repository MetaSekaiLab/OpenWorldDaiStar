using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Features.Home;

public static class HomeEndpoints
{
    private static readonly LoginBonusResult[] EmptyLoginBonuses = [];
    private static readonly NotificationResult[] EmptyNotifications = [];
    private static readonly EndpointContract NotificationContract = new(
        FrameCompression.Standard,
        FrameCompression.Lz4BlockArray,
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Standard);

    public static IEndpointRouteBuilder MapHomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/Home/CheckReceiveLoginBonus", CheckReceiveLoginBonusAsync);
        endpoints.MapPost("/api/Home/CheckEexternalPayment", CheckEexternalPaymentAsync);
        endpoints.MapPost("/api/Home/GetNotificationsAsync", GetNotificationsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetNotificationsAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Home.GetNotificationsAsync").LogInformation(
            "Home.GetNotificationsAsync account_id={AccountId} result_count=0 success=true",
            session.Account.Id);
        return responseWriter.Success(EmptyNotifications, NotificationContract);
    }

    private static async Task<IResult> CheckReceiveLoginBonusAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Home.CheckReceiveLoginBonus").LogInformation(
            "Home.CheckReceiveLoginBonus account_id={AccountId} result_count=0 success=true",
            session.Account.Id);
        return responseWriter.Success(EmptyLoginBonuses, EndpointContract.Standard);
    }

    private static async Task<IResult> CheckEexternalPaymentAsync(
        HttpRequest request,
        SessionAuthenticator authenticator,
        ApiResponseWriter responseWriter,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var session = await authenticator.AuthenticateAsync(request, cancellationToken);
        if (session is null)
            return Results.Unauthorized();

        loggerFactory.CreateLogger("Home.CheckEexternalPayment").LogInformation(
            "Home.CheckEexternalPayment account_id={AccountId} received_count=0 success=true",
            session.Account.Id);
        return responseWriter.Success(
            new EexternalPaymentResult { ReceivedJewelShopItemMasterIds = [] },
            EndpointContract.Standard);
    }
}
