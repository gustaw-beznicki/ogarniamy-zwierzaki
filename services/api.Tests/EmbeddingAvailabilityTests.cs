using ogarniamy_zwierzaki_api.DocumentEmbedding;

namespace ogarniamy_zwierzaki_api.Tests;

// Rate settings outside their range make embedding unavailable instead of failing later in the worker or in search.
public sealed class EmbeddingAvailabilityTests
{
    private static EmbeddingOptions Valid() => new()
    {
        Auth = EmbeddingAuthMode.AzureManagedIdentity,
        Endpoint = new Uri("https://oai-test.openai.azure.com/"),
    };

    [Fact]
    public void Default_rate_settings_are_valid()
    {
        Assert.True(EmbeddingAvailability.Resolve(Valid(), isDevelopment: false).IsAvailable);
    }

    [Theory]
    [InlineData(0, 40_000)]
    [InlineData(250_001, 40_000)]
    [InlineData(16_000, -1)]
    public void Out_of_range_rate_settings_make_embedding_unavailable(int maxEstimatedTokensPerRequest, int workerTokensPerMinute)
    {
        var options = new EmbeddingOptions
        {
            Auth = EmbeddingAuthMode.AzureManagedIdentity,
            Endpoint = new Uri("https://oai-test.openai.azure.com/"),
            MaxEstimatedTokensPerRequest = maxEstimatedTokensPerRequest,
            WorkerTokensPerMinute = workerTokensPerMinute,
        };

        var availability = EmbeddingAvailability.Resolve(options, isDevelopment: false);

        Assert.False(availability.IsAvailable);
        Assert.False(availability.IsDisabled);
    }
}
