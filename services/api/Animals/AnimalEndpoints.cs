using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Animals;

// Animal routes. Data access goes through OwnedAnimals only, never through the DbSets. Mutations require an
// antiforgery token; name and activity edits also require If-Match with the quoted version from the animal response.
// There is no delete route: the MVP never deletes animals.
public static class AnimalEndpoints
{
    public static IEndpointRouteBuilder MapAnimalEndpoints(this IEndpointRouteBuilder app)
    {
        var animals = app.MapGroup("/api/animals");

        animals.MapGet("/", ListAsync).WithName("ListAnimals");
        animals.MapGet("/{id:guid}", GetAsync).WithName("GetAnimal");
        animals.MapPost("/", CreateAsync).WithName("CreateAnimal").AddEndpointFilter<AntiforgeryHeaderFilter>();
        animals.MapPut("/{id:guid}/name", RenameAsync).WithName("RenameAnimal").AddEndpointFilter<AntiforgeryHeaderFilter>();
        animals.MapPut("/{id:guid}/activity", SetActivityAsync).WithName("SetAnimalActivity")
            .AddEndpointFilter<AntiforgeryHeaderFilter>();

        return app;
    }

    // Active animals by default; includeInactive=true returns every owned animal for management and grouping.
    private static async Task<Ok<IReadOnlyList<OwnedAnimal>>> ListAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals, CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        var userId = CurrentUser.Id(principal, users);
        return TypedResults.Ok(includeInactive
            ? await animals.ListAsync(userId, cancellationToken)
            : await animals.ListActiveAsync(userId, cancellationToken));
    }

    private static async Task<Results<Ok<OwnedAnimal>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals,
        CancellationToken cancellationToken)
    {
        // Another account's animal is indistinguishable from a missing one.
        var animal = await animals.FindAsync(CurrentUser.Id(principal, users), id, cancellationToken);
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

        var animal = await animals.CreateAsync(CurrentUser.Id(principal, users), name, cancellationToken);
        return TypedResults.Created($"/api/animals/{animal.Id}", animal);
    }

    private static async Task<Results<Ok<OwnedAnimal>, NotFound, ProblemHttpResult>> RenameAsync(
        Guid id, RenameAnimalRequest request, HttpRequest http, ClaimsPrincipal principal, UserManager<AppUser> users,
        OwnedAnimals animals, CancellationToken cancellationToken)
    {
        var version = ReadVersion(http, out var versionProblem);
        if (versionProblem is not null)
        {
            return versionProblem;
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "name_required");
        }

        if (name.Length > Animal.NameMaxLength)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "name_too_long");
        }

        var result = await animals.RenameAsync(CurrentUser.Id(principal, users), id, version, name, cancellationToken);
        return ToResponse(result);
    }

    private static async Task<Results<Ok<OwnedAnimal>, NotFound, ProblemHttpResult>> SetActivityAsync(
        Guid id, HttpRequest http, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedAnimals animals,
        CancellationToken cancellationToken)
    {
        var version = ReadVersion(http, out var versionProblem);
        if (versionProblem is not null)
        {
            return versionProblem;
        }

        // Read by hand so a malformed body answers with this route's own code instead of a generic 400.
        SetAnimalActivityRequest? request;
        try
        {
            request = await http.ReadFromJsonAsync<SetAnimalActivityRequest>(cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_activity");
        }

        if (request?.IsActive is not { } isActive)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_activity");
        }

        var result = await animals.SetActivityAsync(CurrentUser.Id(principal, users), id, version, isActive, cancellationToken);
        return ToResponse(result);
    }

    private static Results<Ok<OwnedAnimal>, NotFound, ProblemHttpResult> ToResponse(AnimalMutationResult result) =>
        result.Outcome switch
        {
            AnimalMutationOutcome.Updated => TypedResults.Ok(result.Animal!),
            AnimalMutationOutcome.Stale => ApiProblem.Create(StatusCodes.Status412PreconditionFailed, "animal_changed"),
            _ => TypedResults.NotFound(),
        };

    // If-Match must carry exactly one strong entity tag: the quoted version UUID from the animal response.
    private static Guid ReadVersion(HttpRequest http, out ProblemHttpResult? problem)
    {
        problem = null;
        var values = http.Headers.IfMatch;
        if (values.Count == 0)
        {
            problem = ApiProblem.Create(StatusCodes.Status428PreconditionRequired, "version_required");
            return Guid.Empty;
        }

        if (values.Count == 1
            && EntityTagHeaderValue.TryParse(values[0], out var tag)
            && !tag.IsWeak
            && tag.Tag.Length > 2
            && tag.Tag.StartsWith('"')
            && tag.Tag.EndsWith('"')
            && Guid.TryParseExact(tag.Tag.AsSpan(1, tag.Tag.Length - 2), "D", out var version))
        {
            return version;
        }

        problem = ApiProblem.Create(StatusCodes.Status400BadRequest, "invalid_version");
        return Guid.Empty;
    }
}
