using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace ogarniamy_zwierzaki_api.Auth;

// Register, sign-in and sign-out only. MapIdentityApi is not used because it would also expose password reset,
// email confirmation and 2FA routes that this product does not support.
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").AllowAnonymous();

        auth.MapPost("/register", RegisterAsync).WithName("Register");
        auth.MapPost("/login", LoginAsync).WithName("Login");
        auth.MapPost("/logout", LogoutAsync).WithName("Logout");

        return app;
    }

    private static async Task<Results<Created, ProblemHttpResult>> RegisterAsync(
        CredentialsRequest request, UserManager<AppUser> users, SignInManager<AppUser> signIn)
    {
        var email = request.Email ?? string.Empty;
        var user = new AppUser { UserName = email, Email = email };

        var result = await users.CreateAsync(user, request.Password ?? string.Empty);
        if (!result.Succeeded)
        {
            return ToProblem(result.Errors);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return TypedResults.Created();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LoginAsync(
        CredentialsRequest request, SignInManager<AppUser> signIn)
    {
        // An unknown email fails the same way as a wrong password. Identity locks the account on the failure
        // that reaches the limit, so that attempt already reports locked_out.
        var result = await signIn.PasswordSignInAsync(
            request.Email ?? string.Empty,
            request.Password ?? string.Empty,
            isPersistent: true,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return TypedResults.NoContent();
        }

        return ApiProblem.Create(
            StatusCodes.Status401Unauthorized, result.IsLockedOut ? "locked_out" : "invalid_credentials");
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<AppUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult ToProblem(IEnumerable<IdentityError> errors)
    {
        var codes = errors.Select(e => e.Code).ToHashSet();
        var describer = new IdentityErrorDescriber();

        if (codes.Contains(nameof(describer.DuplicateEmail)) || codes.Contains(nameof(describer.DuplicateUserName)))
        {
            return ApiProblem.Create(StatusCodes.Status409Conflict, "email_taken");
        }

        if (codes.Contains(nameof(describer.InvalidEmail)) || codes.Contains(nameof(describer.InvalidUserName)))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_email");
        }

        if (codes.Contains(nameof(describer.PasswordTooShort)))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "password_too_short");
        }

        // Not reachable with the configured options; kept so an unexpected Identity error is still a 400.
        return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_registration");
    }
}
