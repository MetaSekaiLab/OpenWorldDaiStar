using System.Buffers;
using MessagePack;

namespace OpenWorldDaiStar.Wire;

public sealed class ApiRequestReader
{
    private static readonly MessagePackSerializerOptions ClientOptions =
        MessagePackSerializerOptions.Standard.WithCompression(
            MessagePackCompression.Lz4BlockArray);

    public async Task<T> ReadAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken) where T : class
    {
        var streamReader = new MessagePackStreamReader(request.Body);
        var frame = await streamReader.ReadAsync(cancellationToken);
        if (frame is null)
            throw new ApiRequestException("The MessagePack request body is empty.");
        if (await streamReader.ReadAsync(cancellationToken) is not null)
            throw new ApiRequestException("The MessagePack request body contains multiple values.");

        try
        {
            return MessagePackSerializer.Deserialize<T>(frame.Value, ClientOptions)
                   ?? throw new ApiRequestException(
                       $"The MessagePack request body decoded to null {typeof(T).Name}.");
        }
        catch (MessagePackSerializationException exception)
        {
            throw new ApiRequestException(
                $"The MessagePack request body is not a valid {typeof(T).Name}.", exception);
        }
    }
}

public sealed class ApiRequestException(string message, Exception? innerException = null)
    : Exception(message, innerException);
