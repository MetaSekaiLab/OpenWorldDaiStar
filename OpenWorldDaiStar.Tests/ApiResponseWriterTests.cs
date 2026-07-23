using System.Buffers;
using MessagePack;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OpenWorldDaiStar.App;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;
using SiriusApi.Shared.Models.Player;

namespace OpenWorldDaiStar.Tests;

public sealed class ApiResponseWriterTests
{
    [Fact]
    public async Task Success_WritesFiveFramesAndConfiguredHeaders()
    {
        var writer = CreateWriter();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var payload = new MasterDataManifest
        {
            Uri = "mastermemory.db",
            SasToken = string.Empty,
            Version = "123_123",
            PublishTimestamp = 123
        };

        await writer.Success(payload, EndpointContract.Standard).ExecuteAsync(context);

        Assert.Equal("application/vnd.msgpack", context.Response.ContentType);
        Assert.Equal("123_123", context.Response.Headers["X-MasterData-Version"]);
        Assert.Equal("mastermemory.db", context.Response.Headers["X-MasterData-Uri"]);
        Assert.Equal("123", context.Response.Headers["X-MasterData-PublishTimestamp"]);
        context.Response.Body.Position = 0;
        var reader = new MessagePackStreamReader(context.Response.Body);
        Assert.Empty(Deserialize<Fault[]>(await ReadFrameAsync(reader)));
        var actual = Deserialize<MasterDataManifest>(await ReadFrameAsync(reader));
        Assert.Equal(payload.Version, actual.Version);
        Assert.Empty(Deserialize<IDataObject[]>(await ReadFrameAsync(reader)));
        Assert.Empty(Deserialize<DeletedDataObject[]>(await ReadFrameAsync(reader)));
        Assert.Empty(Deserialize<INotificationObject[]>(await ReadFrameAsync(reader)));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Success_WritesProvidedUpdatesWithConfiguredCompression()
    {
        var writer = CreateWriter();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var contract = new EndpointContract(
            FrameCompression.Standard,
            FrameCompression.Standard,
            FrameCompression.Lz4BlockArray,
            FrameCompression.Standard,
            FrameCompression.Standard);

        await writer.Success(
            new BooleanResult { IsSuccess = true },
            [new User { Id = 42, SplashLastDisplayedAt = DateTime.UtcNow }],
            contract).ExecuteAsync(context);

        context.Response.Body.Position = 0;
        var reader = new MessagePackStreamReader(context.Response.Body);
        Assert.Empty(Deserialize<Fault[]>(await ReadFrameAsync(reader)));
        Assert.True(Deserialize<BooleanResult>(await ReadFrameAsync(reader)).IsSuccess);
        var lz4 = MessagePackSerializerOptions.Standard.WithCompression(
            MessagePackCompression.Lz4BlockArray);
        var updates = MessagePackSerializer.Deserialize<IDataObject[]>(
            await ReadFrameAsync(reader), lz4);
        Assert.Equal(42, Assert.Single(updates.OfType<User>()).Id);
        Assert.Empty(Deserialize<DeletedDataObject[]>(await ReadFrameAsync(reader)));
        Assert.Empty(Deserialize<INotificationObject[]>(await ReadFrameAsync(reader)));
        Assert.Null(await reader.ReadAsync(CancellationToken.None));
    }

    private static ApiResponseWriter CreateWriter() =>
        new(Options.Create(new GameOptions
        {
            MasterDataVersion = "123_123",
            MasterDataUri = "mastermemory.db",
            MasterDataPublishTimestamp = 123
        }));

    private static async Task<ReadOnlySequence<byte>> ReadFrameAsync(
        MessagePackStreamReader reader) =>
        await reader.ReadAsync(CancellationToken.None)
        ?? throw new InvalidDataException("Expected another MessagePack frame.");

    private static T Deserialize<T>(ReadOnlySequence<byte> bytes) =>
        MessagePackSerializer.Deserialize<T>(bytes);
}
