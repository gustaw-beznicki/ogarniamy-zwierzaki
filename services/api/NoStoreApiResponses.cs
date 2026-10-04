namespace ogarniamy_zwierzaki_api;

// Marks every /api/* response as not storable, so no browser, proxy or CDN keeps a session-dependent answer.
// An endpoint that sets its own Cache-Control (for example `private, no-store` on originals) keeps it.
public static class NoStoreApiResponses
{
    public static IApplicationBuilder UseNoStoreApiResponses(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                var response = context.Response;
                response.OnStarting(() =>
                {
                    if (string.IsNullOrEmpty(response.Headers.CacheControl))
                    {
                        response.Headers.CacheControl = "no-store";
                    }

                    return Task.CompletedTask;
                });
            }

            await next(context);
        });
}
