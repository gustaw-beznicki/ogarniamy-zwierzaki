using Azure.Core;

namespace ogarniamy_zwierzaki_api.Tests;

// Returns a fixed bearer token without contacting Entra ID.
public sealed class FakeTokenCredential : TokenCredential
{
    public const string Token = "test-token";

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(Token, DateTimeOffset.UtcNow.AddHours(1));

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}
