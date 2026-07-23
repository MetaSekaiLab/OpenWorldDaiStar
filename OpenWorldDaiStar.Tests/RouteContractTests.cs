using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace OpenWorldDaiStar.Tests;

public sealed class RouteContractTests
{
    [Theory]
    [InlineData("/api/Environment", "POST")]
    [InlineData("/api/data/master", "GET")]
    [InlineData("/api/Account/Register", "POST")]
    [InlineData("/api/Account/Authenticate", "POST")]
    [InlineData("/api/Login", "POST")]
    [InlineData("/api/data/user", "GET")]
    [InlineData("/api/Home/CheckReceiveLoginBonus", "POST")]
    [InlineData("/api/Inboxes/CheckPackagesAsync", "POST")]
    [InlineData("/api/ReceivedRequest", "POST")]
    [InlineData("/api/Circles/Invited", "GET")]
    [InlineData("/api/FriendInvitation/Update", "POST")]
    [InlineData("/api/Home/CheckEexternalPayment", "POST")]
    [InlineData("/api/Home/GetNotificationsAsync", "POST")]
    [InlineData("/api/Player/UpdateGameHintRead", "POST")]
    [InlineData("/api/Player/UpdateSplashLastDisplayTime", "POST")]
    public async Task BootstrapRoute_UsesClientConfirmedMethod(string path, string method)
    {
        await using var app = OpenWorldDaiStar.Program.BuildApplication([]);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>();
        var endpoint = Assert.Single(endpoints, endpoint =>
            endpoint.RoutePattern.RawText == path);
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;

        Assert.Equal([method], methods);
    }
}
