namespace OpenWorldDaiStar.Identity;

public sealed class SessionAuthenticator(IdentityService identities)
{
    public Task<AccountSession?> AuthenticateAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var token = authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
        return identities.ValidateSessionAsync(token, cancellationToken);
    }
}
