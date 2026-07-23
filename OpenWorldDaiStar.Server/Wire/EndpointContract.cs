namespace OpenWorldDaiStar.Wire;

public enum FrameCompression
{
    Standard,
    Lz4BlockArray
}

public sealed record EndpointContract(
    FrameCompression Faults,
    FrameCompression Payload,
    FrameCompression Updates,
    FrameCompression Deletes,
    FrameCompression Effects)
{
    public static EndpointContract Standard { get; } = new(
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Standard,
        FrameCompression.Standard);
}
