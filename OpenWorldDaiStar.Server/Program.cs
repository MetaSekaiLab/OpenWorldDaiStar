using OpenWorldDaiStar.App;
using OpenWorldDaiStar.Features.Account;
using OpenWorldDaiStar.Features.Circles;
using OpenWorldDaiStar.Features.Environment;
using OpenWorldDaiStar.Features.FriendInvitation;
using OpenWorldDaiStar.Features.Friends;
using OpenWorldDaiStar.Features.Home;
using OpenWorldDaiStar.Features.Inboxes;
using OpenWorldDaiStar.Features.Login;
using OpenWorldDaiStar.Features.MasterData;
using OpenWorldDaiStar.Features.Player;
using OpenWorldDaiStar.Features.UserData;
using OpenWorldDaiStar.Identity;
using OpenWorldDaiStar.Wire;

namespace OpenWorldDaiStar;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = BuildApplication(args);
        await app.RunAsync();
        return 0;
    }

    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = ResolveContentRoot()
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenWorldConfiguration(builder.Configuration);
        builder.Services.AddSingleton<IdentityService>();
        builder.Services.AddSingleton<SessionAuthenticator>();
        builder.Services.AddSingleton<ApiRequestReader>();
        builder.Services.AddSingleton<ApiResponseWriter>();

        var apiUrl = builder.Configuration[$"{ServerOptions.SectionName}:ApiUrl"]
                     ?? ServerOptions.DefaultApiUrl;
        builder.WebHost.UseUrls(apiUrl);

        var app = builder.Build();
        var appLogger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OpenWorldDaiStar");

        app.Use(async (context, next) =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await next();
            stopwatch.Stop();
            appLogger.LogInformation(
                "{Method} {Path}{QueryString} → {StatusCode} ({Elapsed}ms)",
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        });
        app.UseExceptionHandler();
        app.MapInfrastructureEndpoints();
        app.MapEnvironmentEndpoints();
        app.MapMasterDataEndpoints();
        app.MapAccountEndpoints();
        app.MapLoginEndpoints();
        app.MapUserDataEndpoints();
        app.MapHomeEndpoints();
        app.MapInboxesEndpoints();
        app.MapFriendInvitationEndpoints();
        app.MapFriendsEndpoints();
        app.MapCirclesEndpoints();
        app.MapPlayerEndpoints();
        return app;
    }

    internal static string ResolveContentRoot()
    {
        var currentDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        if (File.Exists(Path.Combine(currentDirectory, "OpenWorldDaiStar.sln")))
            return currentDirectory;

        var parentDirectory = Directory.GetParent(currentDirectory)?.FullName;
        if (parentDirectory is not null &&
            File.Exists(Path.Combine(parentDirectory, "OpenWorldDaiStar.sln")))
        {
            return parentDirectory;
        }

        var applicationDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        if (File.Exists(Path.Combine(applicationDirectory, "appsettings.json")))
            return applicationDirectory;

        throw new InvalidOperationException(
            "Cannot locate OpenWorldDaiStar content root. Start from the repository root, " +
            "the OpenWorldDaiStar.Server directory, or a published directory containing appsettings.json.");
    }
}
