namespace ogarniamy_zwierzaki_api.Auth;

// One signed-in device. The session cookie carries only this row's ID; the authentication ticket stays on the
// server, so deleting the row ends the session even if a copy of the cookie survives (for example when a proxy
// drops the expiring Set-Cookie of a logout).
public sealed class AuthSession
{
    // How long a session lasts without activity; each renewal of the sliding expiration extends it again.
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    // The serialized authentication ticket, encrypted with Data Protection.
    public byte[] Ticket { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastRenewedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}
