using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ogarniamy_zwierzaki_api.Animals;

namespace ogarniamy_zwierzaki_api.Auth;

// The current session, used by the UI for gating and onboarding.
public static class MeEndpoint
{
    public static IEndpointRouteBuilder MapMeEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", GetMeAsync).WithName("GetMe");
        return app;
    }

    private static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var count = await animals.CountAsync(user.Id, cancellationToken);
        return TypedResults.Ok(new MeResponse(user.Email ?? string.Empty, count > 0));
    }
}
