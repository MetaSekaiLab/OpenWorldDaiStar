using MessagePack;
using Microsoft.AspNetCore.Http;
using OpenWorldDaiStar.Wire;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Tests;

public sealed class ApiRequestReaderTests
{
    private static readonly MessagePackSerializerOptions Lz4 =
        MessagePackSerializerOptions.Standard.WithCompression(
            MessagePackCompression.Lz4BlockArray);

    [Fact]
    public async Task ReadAsync_DecodesClientLz4Payload()
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(MessagePackSerializer.Serialize(
            new RegisterPayload { Name = "DaiStar" }, Lz4));

        var actual = await new ApiRequestReader().ReadAsync<RegisterPayload>(
            context.Request, CancellationToken.None);

        Assert.Equal("DaiStar", actual.Name);
    }

    [Fact]
    public async Task ReadAsync_RejectsMultipleMessagePackValues()
    {
        var body = new MemoryStream();
        await MessagePackSerializer.SerializeAsync(
            body, new RegisterPayload { Name = "First" }, Lz4);
        await MessagePackSerializer.SerializeAsync(
            body, new RegisterPayload { Name = "Second" }, Lz4);
        body.Position = 0;
        var context = new DefaultHttpContext();
        context.Request.Body = body;

        await Assert.ThrowsAsync<ApiRequestException>(() =>
            new ApiRequestReader().ReadAsync<RegisterPayload>(
                context.Request, CancellationToken.None));
    }
}
