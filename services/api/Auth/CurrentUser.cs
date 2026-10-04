using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace ogarniamy_zwierzaki_api.Auth;

// The signed-in user's id for endpoints behind the fallback policy.
public static class CurrentUser
{
    // The fallback policy guarantees an authenticated user, so a missing id is a server error.
    public static string Id(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        users.GetUserId(principal) ?? throw new InvalidOperationException("The authenticated user has no id claim.");
}
