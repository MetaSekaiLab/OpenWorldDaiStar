using System.Buffers;
using System.Net.Http.Headers;
using MessagePack;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SiriusApi.Shared;
using SiriusApi.Shared.Models.Player;

namespace OpenWorldDaiStar.Tests;

public sealed class AnonymousBootstrapFlowTests
{
    private static readonly MessagePackSerializerOptions Standard =
        MessagePackSerializerOptions.Standard;
    private static readonly MessagePackSerializerOptions Lz4 =
        MessagePackSerializerOptions.Standard.WithCompression(
            MessagePackCompression.Lz4BlockArray);

    [Fact]
    public async Task RegisterAuthenticateLoginAndStatelessEndpoints_CompleteOverHttp()
    {
        await using var app = OpenWorldDaiStar.Program.BuildApplication(
        [
            "--environment",
            "Development"
        ]);
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };

            using var registerResponse = await PostAsync(
                client,
                "/api/Account/Register",
                new RegisterPayload { Name = "HTTP User" });
            var registration = await ReadPayloadAsync<AccountRegistResult>(
                registerResponse, Standard);
            Assert.StartsWith("login_", registration.Token);

            var protectedRoutes = new (HttpMethod Method, string Path)[]
            {
                (HttpMethod.Post, "/api/Home/CheckReceiveLoginBonus"),
                (HttpMethod.Post, "/api/Inboxes/CheckPackagesAsync"),
                (HttpMethod.Post, "/api/ReceivedRequest"),
                (HttpMethod.Get, "/api/Circles/Invited"),
                (HttpMethod.Post, "/api/FriendInvitation/Update"),
                (HttpMethod.Post, "/api/Home/CheckEexternalPayment"),
                (HttpMethod.Post, "/api/Player/UpdateGameHintRead"),
                (HttpMethod.Post, "/api/Home/GetNotificationsAsync"),
                (HttpMethod.Post, "/api/Player/UpdateSplashLastDisplayTime")
            };
            foreach (var (method, path) in protectedRoutes)
            {
                using var unauthorizedRequest = new HttpRequestMessage(method, path);
                using var unauthorizedResponse = await client.SendAsync(unauthorizedRequest);
                Assert.Equal(
                    System.Net.HttpStatusCode.Unauthorized,
                    unauthorizedResponse.StatusCode);
            }

            using var authenticateResponse = await PostAsync(
                client,
                "/api/Account/Authenticate",
                new AuthenticatePayload
                {
                    LoginToken = registration.Token,
                    GameVersion = GameVersions.GooglePlay,
                    ApkHash = string.Empty,
                    ApkApplicationSignature = string.Empty,
                    ApplicationVersion = "2.31.0"
                });
            var authentication = await ReadPayloadAsync<AuthenticateResult>(
                authenticateResponse, Standard);
            Assert.StartsWith("sess_", authentication.Token);

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authentication.Token);
            using var loginResponse = await PostAsync(
                client,
                "/api/Login",
                new LoginPayload { PushNotificationToken = string.Empty });
            var login = await ReadPayloadAsync<LoginResult>(loginResponse, Standard);
            Assert.Empty(login.InvalidedStarPasses);

            using var loginBonusResponse = await client.PostAsync(
                "/api/Home/CheckReceiveLoginBonus", content: null);
            var loginBonuses = await ReadPayloadAsync<LoginBonusResult[]>(
                loginBonusResponse, Standard);
            Assert.Empty(loginBonuses);

            using var inboxResponse = await client.PostAsync(
                "/api/Inboxes/CheckPackagesAsync", content: null);
            var inboxPackages = await ReadPayloadAsync<BooleanResult>(
                inboxResponse, Standard);
            Assert.False(inboxPackages.IsSuccess);

            using var receivedRequestResponse = await client.PostAsync(
                "/api/ReceivedRequest", content: null);
            var receivedRequests = await ReadPayloadAsync<FriendListResult>(
                receivedRequestResponse, Standard);
            Assert.Empty(receivedRequests.Results);
            Assert.Equal(0, receivedRequests.CurrentFriendCount);

            using var invitedCirclesResponse = await client.GetAsync("/api/Circles/Invited");
            var invitedCircles = await ReadPayloadAsync<CircleInformationResult[]>(
                invitedCirclesResponse, Standard);
            Assert.Empty(invitedCircles);

            using var externalPaymentResponse = await client.PostAsync(
                "/api/Home/CheckEexternalPayment", content: null);
            var externalPayment = await ReadPayloadAsync<EexternalPaymentResult>(
                externalPaymentResponse, Standard);
            Assert.Empty(externalPayment.ReceivedJewelShopItemMasterIds);

            using var invalidGameHintResponse = await PostAsync(
                client,
                "/api/Player/UpdateGameHintRead",
                new UpdateGameHintPayload { Categories = [] });
            Assert.Equal(
                System.Net.HttpStatusCode.BadRequest,
                invalidGameHintResponse.StatusCode);

            using var gameHintResponse = await PostAsync(
                client,
                "/api/Player/UpdateGameHintRead",
                new UpdateGameHintPayload { Categories = [PageCategories.Photo] });
            var gameHintResult = await ReadPayloadAsync<BooleanResult>(
                gameHintResponse,
                Standard);
            Assert.True(gameHintResult.IsSuccess);

            using var notificationsResponse = await client.PostAsync(
                "/api/Home/GetNotificationsAsync", content: null);
            var notifications = await ReadPayloadAsync<NotificationResult[]>(
                notificationsResponse,
                Lz4);
            Assert.Empty(notifications);

            using var splashDisplayResponse = await client.PostAsync(
                "/api/Player/UpdateSplashLastDisplayTime", content: null);
            var (splashDisplayResult, splashUpdates) =
                await ReadPayloadWithUpdatesAsync<BooleanResult>(
                    splashDisplayResponse,
                    Standard,
                    Lz4);
            Assert.True(splashDisplayResult.IsSuccess);
            var updatedUser = Assert.Single(splashUpdates.OfType<User>());
            Assert.NotEqual(DateTime.MinValue, updatedUser.SplashLastDisplayedAt);

            using var userDataResponse = await client.GetAsync("/api/data/user");
            var userData = await ReadPayloadAsync<IDataObject[]>(userDataResponse, Lz4);
            Assert.Empty(userData);

            using var friendInvitationResponse = await client.PostAsync(
                "/api/FriendInvitation/Update", content: null);
            var friendInvitation = await ReadPayloadAsync<BooleanResult>(
                friendInvitationResponse,
                Standard);
            Assert.True(friendInvitation.IsSuccess);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static Task<HttpResponseMessage> PostAsync<T>(
        HttpClient client,
        string path,
        T payload)
    {
        var content = new ByteArrayContent(MessagePackSerializer.Serialize(payload, Lz4));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.msgpack");
        return client.PostAsync(path, content);
    }

    private static async Task<T> ReadPayloadAsync<T>(
        HttpResponseMessage response,
        MessagePackSerializerOptions payloadOptions)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status {(int)response.StatusCode}: {error}", null, response.StatusCode);
        }
        await using var stream = await response.Content.ReadAsStreamAsync();
        var reader = new MessagePackStreamReader(stream);
        Assert.Empty(MessagePackSerializer.Deserialize<Fault[]>(
            await ReadFrameAsync(reader), Standard));
        var payload = MessagePackSerializer.Deserialize<T>(
            await ReadFrameAsync(reader), payloadOptions);
        Assert.Empty(MessagePackSerializer.Deserialize<IDataObject[]>(
            await ReadFrameAsync(reader), Standard));
        Assert.Empty(MessagePackSerializer.Deserialize<DeletedDataObject[]>(
            await ReadFrameAsync(reader), Standard));
        Assert.Empty(MessagePackSerializer.Deserialize<INotificationObject[]>(
            await ReadFrameAsync(reader), Standard));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
        return payload;
    }

    private static async Task<(T Payload, IDataObject[] Updates)>
        ReadPayloadWithUpdatesAsync<T>(
            HttpResponseMessage response,
            MessagePackSerializerOptions payloadOptions,
            MessagePackSerializerOptions updatesOptions)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status {(int)response.StatusCode}: {error}", null, response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var reader = new MessagePackStreamReader(stream);
        Assert.Empty(MessagePackSerializer.Deserialize<Fault[]>(
            await ReadFrameAsync(reader), Standard));
        var payload = MessagePackSerializer.Deserialize<T>(
            await ReadFrameAsync(reader), payloadOptions);
        var updates = MessagePackSerializer.Deserialize<IDataObject[]>(
            await ReadFrameAsync(reader), updatesOptions);
        Assert.Empty(MessagePackSerializer.Deserialize<DeletedDataObject[]>(
            await ReadFrameAsync(reader), Standard));
        Assert.Empty(MessagePackSerializer.Deserialize<INotificationObject[]>(
            await ReadFrameAsync(reader), Standard));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
        return (payload, updates);
    }

    private static async Task<ReadOnlySequence<byte>> ReadFrameAsync(
        MessagePackStreamReader reader) =>
        await reader.ReadAsync(CancellationToken.None)
        ?? throw new InvalidDataException("Expected another MessagePack response frame.");
}
