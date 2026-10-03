using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Auth;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAppDatabase();
// Originals live in a private Blob container: Azure Storage with the managed identity, Azurite locally and in tests.
builder.Services.AddOriginalStorage();

// Cookie encryption keys live in PostgreSQL, so sessions survive restarts in every hosting mode.
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>();

// Accounts are ASP.NET Core Identity users stored in PostgreSQL; the session is an HttpOnly cookie.
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();
// PBKDF2-HMAC-SHA512 work factor (OWASP minimum: 220,000). Older hashes are re-hashed at the next successful sign-in.
builder.Services.Configure<PasswordHasherOptions>(builder.Configuration.GetSection("PasswordHasher"));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "oz_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    // An API answers with a status code instead of redirecting to a login or access-denied page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Every endpoint requires a signed-in user unless it opts out with AllowAnonymous.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddScoped<OwnedAnimals>();

var app = builder.Build();

await app.PrepareOriginalStorageAsync();
await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Development only, so the document is readable without signing in.
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Healthy only when the database is reachable; an unhealthy check returns 503.
app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var status = report.Status == HealthStatus.Healthy ? "ok" : "unhealthy";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status }));
    },
})
    .WithName("GetHealth")
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapMeEndpoint();
app.MapAnimalEndpoints();

app.Run();
