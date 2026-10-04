using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ogarniamy_zwierzaki_api.Data;

namespace ogarniamy_zwierzaki_api.Auth;

// Keeps the Identity application cookie's tickets in PostgreSQL (auth_sessions), one row per sign-in. The cookie
// handler holds a single instance, so every call opens its own scope for the database context.
public sealed class DatabaseTicketStore(
    IServiceScopeFactory scopes,
    IDataProtectionProvider dataProtection,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider time) : ITicketStore
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("ogarniamy_zwierzaki_api.Auth.AuthSession.v1");

    public Task<string> StoreAsync(AuthenticationTicket ticket) => StoreAsync(ticket, CancellationToken.None);

    public async Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        var userId = ticket.Principal.FindFirst(identityOptions.Value.ClaimsIdentity.UserIdClaimType)?.Value
            ?? throw new InvalidOperationException("The signed-in principal has no user ID claim.");
        var now = time.GetUtcNow();

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Abandoned sessions are never logged out; drop this account's expired ones while it signs in.
        await db.AuthSessions
            .Where(s => s.UserId == userId && s.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);

        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Ticket = Protect(ticket),
            CreatedAt = now,
            ExpiresAt = ExpiresAt(ticket, now),
        };
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session.Id.ToString();
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, CancellationToken.None);

    // Updates only an existing row: a renewal racing a logout must not bring the session back.
    public async Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
        {
            return;
        }

        var now = time.GetUtcNow();
        var protectedTicket = Protect(ticket);
        var expiresAt = ExpiresAt(ticket, now);

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.AuthSessions
            .Where(s => s.Id == id && s.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Ticket, protectedTicket)
                    .SetProperty(s => s.ExpiresAt, expiresAt)
                    .SetProperty(s => s.LastRenewedAt, now),
                cancellationToken);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, CancellationToken.None);

    public async Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
        {
            return null;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await db.AuthSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAt <= time.GetUtcNow())
        {
            await db.AuthSessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
            return null;
        }

        return Unprotect(session.Ticket);
    }

    public Task RemoveAsync(string key) => RemoveAsync(key, CancellationToken.None);

    // The request's cancellation token is ignored: a logout must end the session even if the client gives up waiting.
    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.AuthSessions.Where(s => s.Id == id).ExecuteDeleteAsync(CancellationToken.None);
    }

    // The cookie handler sets ExpiresUtc before storing or renewing; the fallback only guards against a missing value.
    private static DateTimeOffset ExpiresAt(AuthenticationTicket ticket, DateTimeOffset now) =>
        ticket.Properties.ExpiresUtc ?? now.Add(AuthSession.Lifetime);

    private byte[] Protect(AuthenticationTicket ticket) => _protector.Protect(TicketSerializer.Default.Serialize(ticket));

    // A row that no longer decrypts (for example after a key loss) is treated as no session.
    private AuthenticationTicket? Unprotect(byte[] data)
    {
        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(data));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
