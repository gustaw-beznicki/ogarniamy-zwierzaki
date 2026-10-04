using Microsoft.AspNetCore.Identity;

namespace ogarniamy_zwierzaki_api.Auth;

// An account. The email doubles as the Identity user name.
public sealed class AppUser : IdentityUser
{
    // The animal of the account's most recently completed capture, used as the capture default on every device.
    // Null before the first capture; written only by OwnedDocuments when a capture completes.
    public Guid? LastCaptureAnimalId { get; set; }
}
