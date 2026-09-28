using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ogarniamy_zwierzaki_api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAppDatabase();

var app = builder.Build();

await app.MigrateDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
    .WithName("GetHealth");

app.Run();
