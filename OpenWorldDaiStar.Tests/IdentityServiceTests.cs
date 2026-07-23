using Microsoft.Extensions.Logging.Abstractions;
using OpenWorldDaiStar.Identity;

namespace OpenWorldDaiStar.Tests;

public sealed class IdentityServiceTests
{
    [Fact]
    public async Task RegisterAuthenticateAndValidate_UsesOnlyProcessMemory()
    {
        var identities = new IdentityService(NullLogger<IdentityService>.Instance);

        var account = await identities.RegisterAsync("Player One", CancellationToken.None);
        var authenticated = await identities.AuthenticateAsync(
            account.LoginToken,
            CancellationToken.None);
        var session = await identities.ValidateSessionAsync(
            authenticated?.AccessToken,
            CancellationToken.None);

        Assert.StartsWith("login_", account.LoginToken);
        Assert.StartsWith("sess_", authenticated?.AccessToken);
        Assert.Equal(account.AccountId, session?.Account.Id);
        Assert.Equal("Player One", session?.Account.DisplayName);

        var anotherProcess = new IdentityService(NullLogger<IdentityService>.Instance);
        Assert.Null(await anotherProcess.AuthenticateAsync(
            account.LoginToken,
            CancellationToken.None));
        Assert.Null(await anotherProcess.ValidateSessionAsync(
            authenticated?.AccessToken,
            CancellationToken.None));
    }
}
