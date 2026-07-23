using Microsoft.Extensions.Options;

namespace OpenWorldDaiStar.App;

public sealed class ServerOptions
{
    public const string SectionName = "OpenWorld:Server";
    public const string DefaultApiUrl = "http://127.0.0.1:10222";

    public string ApiUrl { get; init; } = DefaultApiUrl;
}

public sealed class GameOptions
{
    public const string SectionName = "OpenWorld:Game";

    public string ApplicationVersion { get; init; } = "2.31.0";
    public int GameVersion { get; init; } = 2;
    public string AssetVersion { get; init; } = "1.96.0";
    public string MasterDataVersion { get; init; } = "0_0";
    public string MasterDataUri { get; init; } = "mastermemory.db";
    public long MasterDataPublishTimestamp { get; init; }
    public string ApiBaseUrl { get; init; } = ServerOptions.DefaultApiUrl;
    public string MaintenanceApiEndpoint { get; init; } = ServerOptions.DefaultApiUrl;
    public string NewsApiEndpoint { get; init; } = ServerOptions.DefaultApiUrl;
    public string MasterDataUrl { get; init; } =
        "https://127.0.0.1:10222/master-data/production";
    public string AssetBaseUrl { get; init; } =
        "https://127.0.0.1:10222/assets/production";
    public string StaticContentUrl { get; init; } =
        "https://127.0.0.1:10222/assets/production/static-assets";
    public string PhotoContentUrl { get; init; } = "https://127.0.0.1:10222/photos";
    public string MultiRealTimeServerUrl { get; init; } = ServerOptions.DefaultApiUrl;
    public string ExternalPaymentUrl { get; init; } = ServerOptions.DefaultApiUrl;
}

public static class ConfigurationExtensions
{
    public static IServiceCollection AddOpenWorldConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ServerOptions>()
            .Bind(configuration.GetSection(ServerOptions.SectionName))
            .Validate(options => IsLoopbackApi(options.ApiUrl),
                "OpenWorld:Server:ApiUrl must be http://127.0.0.1:10222.")
            .ValidateOnStart();

        services.AddOptions<GameOptions>()
            .Bind(configuration.GetSection(GameOptions.SectionName))
            .Validate(options =>
                    options.ApplicationVersion == "2.31.0" &&
                    options.GameVersion == 2 &&
                    !string.IsNullOrWhiteSpace(options.MasterDataVersion) &&
                    IsSafeRelativeUri(options.MasterDataUri) &&
                    options.MasterDataPublishTimestamp >= 0 &&
                    IsLoopbackApi(options.ApiBaseUrl) &&
                    IsLoopbackApi(options.MaintenanceApiEndpoint) &&
                    IsLoopbackApi(options.NewsApiEndpoint) &&
                    IsLoopbackApi(options.MultiRealTimeServerUrl) &&
                    IsLoopbackApi(options.ExternalPaymentUrl) &&
                    IsHttps(options.MasterDataUrl) &&
                    IsHttps(options.AssetBaseUrl) &&
                    IsHttps(options.StaticContentUrl) &&
                    IsHttps(options.PhotoContentUrl),
                "OpenWorld game endpoints or client version are invalid.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsLoopbackApi(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttp &&
        uri.Host == "127.0.0.1" &&
        uri.Port == 10222 &&
        uri.AbsolutePath == "/";

    private static bool IsHttps(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    private static bool IsSafeRelativeUri(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.StartsWith('/') &&
        !value.StartsWith('\\') &&
        !Uri.TryCreate(value, UriKind.Absolute, out _) &&
        value.Split('/', '\\').All(part => part is not ".." and not "." and not "");
}
