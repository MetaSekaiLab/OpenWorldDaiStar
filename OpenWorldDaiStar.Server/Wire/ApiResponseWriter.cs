using MessagePack;
using Microsoft.Extensions.Options;
using OpenWorldDaiStar.App;
using SiriusApi.Shared;

namespace OpenWorldDaiStar.Wire;

public sealed class ApiResponseWriter(IOptions<GameOptions> options)
{
    private readonly GameOptions _game = options.Value;
    private static readonly Fault[] EmptyFaults = [];
    private static readonly IDataObject[] EmptyUpdates = [];
    private static readonly DeletedDataObject[] EmptyDeletes = [];
    private static readonly INotificationObject[] EmptyEffects = [];
    private static readonly MessagePackSerializerOptions Standard =
        MessagePackSerializerOptions.Standard;
    private static readonly MessagePackSerializerOptions Lz4 =
        MessagePackSerializerOptions.Standard.WithCompression(
            MessagePackCompression.Lz4BlockArray);

    public IResult Success<T>(T payload, EndpointContract contract) =>
        Success(payload, EmptyUpdates, contract);

    public IResult Success<T>(
        T payload,
        IDataObject[] updates,
        EndpointContract contract) =>
        new FiveFrameResult<T>(
            this,
            payload,
            EmptyFaults,
            updates,
            contract,
            StatusCodes.Status200OK);

    internal async Task WriteAsync<T>(
        HttpContext context,
        T payload,
        Fault[] faults,
        IDataObject[] updates,
        EndpointContract contract,
        int statusCode)
    {
        var response = context.Response;
        response.StatusCode = statusCode;
        response.ContentType = "application/vnd.msgpack";
        response.Headers["X-FM"] = "0";

        response.Headers["X-MasterData-Version"] = _game.MasterDataVersion;
        response.Headers["X-MasterData-Uri"] = _game.MasterDataUri;
        response.Headers["X-MasterData-SasToken"] = string.Empty;
        response.Headers["X-MasterData-PublishTimestamp"] =
            _game.MasterDataPublishTimestamp.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        await MessagePackSerializer.SerializeAsync(
            response.Body, faults, Options(contract.Faults), context.RequestAborted);
        await MessagePackSerializer.SerializeAsync(
            response.Body, payload, Options(contract.Payload), context.RequestAborted);
        await MessagePackSerializer.SerializeAsync(
            response.Body, updates, Options(contract.Updates), context.RequestAborted);
        await MessagePackSerializer.SerializeAsync(
            response.Body, EmptyDeletes, Options(contract.Deletes), context.RequestAborted);
        await MessagePackSerializer.SerializeAsync(
            response.Body, EmptyEffects, Options(contract.Effects), context.RequestAborted);
    }

    private static MessagePackSerializerOptions Options(FrameCompression compression) =>
        compression == FrameCompression.Lz4BlockArray ? Lz4 : Standard;

    private sealed class FiveFrameResult<T>(
        ApiResponseWriter writer,
        T payload,
        Fault[] faults,
        IDataObject[] updates,
        EndpointContract contract,
        int statusCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) =>
            writer.WriteAsync(httpContext, payload, faults, updates, contract, statusCode);
    }
}
