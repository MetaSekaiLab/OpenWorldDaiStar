namespace OpenWorldDaiStar.Wire;

public static class YmstDateTime
{
    public static DateTime FromUnixMillisecondsAsJstWire(long unixMilliseconds)
    {
        var jst = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds)
            .ToOffset(TimeSpan.FromHours(9));
        return DateTime.SpecifyKind(jst.DateTime, DateTimeKind.Utc);
    }
}
