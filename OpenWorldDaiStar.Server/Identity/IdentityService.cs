using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace OpenWorldDaiStar.Identity;

public sealed class IdentityService(ILogger<IdentityService> logger)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private readonly ConcurrentDictionary<string, AccountIdentity> _accounts =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AccountSession> _sessions =
        new(StringComparer.Ordinal);
    private long _nextAccountId;

    public Task<RegisteredAccount> RegisterAsync(
        string? requestedName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var displayName = string.IsNullOrWhiteSpace(requestedName)
            ? "Player"
            : requestedName.Trim();
        var accountId = Interlocked.Increment(ref _nextAccountId);
        var loginToken = NewToken("login");
        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _accounts[loginToken] = new AccountIdentity(accountId, displayName, createdAt);

        logger.LogInformation(
            "Account.Register account_id={AccountId} name={Name} storage=memory success=true",
            accountId,
            displayName);
        return Task.FromResult(
            new RegisteredAccount(accountId, displayName, loginToken, createdAt));
    }

    public Task<AuthenticatedSession?> AuthenticateAsync(
        string? loginToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(loginToken) ||
            !_accounts.TryGetValue(loginToken.Trim(), out var account))
        {
            logger.LogWarning("Account.Authenticate success=false");
            return Task.FromResult<AuthenticatedSession?>(null);
        }

        var accessToken = NewToken("sess");
        var sessionKey = NewToken("sid");
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime).ToUnixTimeMilliseconds();
        _sessions[accessToken] = new AccountSession(sessionKey, account, expiresAt);

        logger.LogInformation(
            "Account.Authenticate account_id={AccountId} session_key={SessionKey} " +
            "storage=memory success=true",
            account.Id,
            sessionKey);
        return Task.FromResult<AuthenticatedSession?>(new AuthenticatedSession(
            account,
            sessionKey,
            accessToken,
            expiresAt));
    }

    public Task<AccountSession?> ValidateSessionAsync(
        string? accessToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(accessToken))
            return Task.FromResult<AccountSession?>(null);

        var token = accessToken.Trim();
        if (!_sessions.TryGetValue(token, out var session))
            return Task.FromResult<AccountSession?>(null);

        if (session.ExpiresAtUnixMs > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            return Task.FromResult<AccountSession?>(session);

        _sessions.TryRemove(token, out _);
        return Task.FromResult<AccountSession?>(null);
    }

    private static string NewToken(string prefix) =>
        prefix + "_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record RegisteredAccount(
    long AccountId,
    string DisplayName,
    string LoginToken,
    long CreatedAtUnixMs);

public sealed record AccountIdentity(long Id, string DisplayName, long CreatedAtUnixMs);

public sealed record AuthenticatedSession(
    AccountIdentity Account,
    string SessionKey,
    string AccessToken,
    long ExpiresAtUnixMs);

public sealed record AccountSession(
    string SessionKey,
    AccountIdentity Account,
    long ExpiresAtUnixMs);
