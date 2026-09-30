using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Animals;

// Animal routes. Data access goes through OwnedAnimals only, never through the DbSets.
public static class AnimalEndpoints
{
    public static IEndpointRouteBuilder MapAnimalEndpoints(this IEndpointRouteBuilder app)
    {
        var animals = app.MapGroup("/api/animals");

        animals.MapGet("/", ListAsync).WithName("ListAnimals");
        animals.MapGet("/{id:guid}", GetAsync).WithName("GetAnimal");
        animals.MapPost("/", CreateAsync).WithName("CreateAnimal");

        return app;
    }

    private static async Task<Ok<IReadOnlyList<OwnedAnimal>>> ListAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals, CancellationToken cancellationToken) =>
        TypedResults.Ok(await animals.ListAsync(CurrentUserId(principal, users), cancellationToken));

    private static async Task<Results<Ok<OwnedAnimal>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals,
        CancellationToken cancellationToken)
    {
        // Another account's animal is indistinguishable from a missing one.
        var animal = await animals.FindAsync(CurrentUserId(principal, users), id, cancellationToken);
        return animal is null ? TypedResults.NotFound() : TypedResults.Ok(animal);
    }

    private static async Task<Results<Created<OwnedAnimal>, ProblemHttpResult>> CreateAsync(
        CreateAnimalRequest request, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "name_required");
        }

        if (name.Length > Animal.NameMaxLength)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "name_too_long");
        }

        var animal = await animals.CreateAsync(CurrentUserId(principal, users), name, cancellationToken);
        return TypedResults.Created($"/api/animals/{animal.Id}", animal);
    }

    // The fallback policy guarantees an authenticated user, so a missing id is a server error.
    private static string CurrentUserId(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        users.GetUserId(principal) ?? throw new InvalidOperationException("The authenticated user has no id claim.");
}
