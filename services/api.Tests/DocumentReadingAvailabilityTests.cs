using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// Reading configuration never stops the API: every invalid setup resolves to "unavailable" with a reason.
public sealed class DocumentReadingAvailabilityTests
{
    private static readonly Uri ValidEndpoint = new("https://di-test.cognitiveservices.azure.com/");

    [Fact]
    public void Disabled_is_the_default_and_is_not_reported_as_misconfiguration()
    {
        var availability = DocumentReadingAvailability.Resolve(new DocumentReadingOptions(), isDevelopment: true);

        Assert.False(availability.IsAvailable);
        Assert.True(availability.IsDisabled);
        Assert.Null(availability.Credential);
        Assert.False(availability.Options.WorkerEnabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/relative")]
    [InlineData("http://di-test.cognitiveservices.azure.com/")]
    public void An_endpoint_that_is_not_absolute_https_makes_reading_unavailable(string? endpoint)
    {
        var options = new DocumentReadingOptions
        {
            Auth = DocumentReadingAuthMode.AzureManagedIdentity,
            Endpoint = endpoint is null ? null : new Uri(endpoint, UriKind.RelativeOrAbsolute),
        };

        var availability = DocumentReadingAvailability.Resolve(options, isDevelopment: false);

        Assert.False(availability.IsAvailable);
        Assert.False(availability.IsDisabled);
        Assert.Contains("DocumentReading:Endpoint", availability.Reason);
    }

    [Fact]
    public void Developer_credentials_are_refused_outside_development()
    {
        var options = new DocumentReadingOptions { Auth = DocumentReadingAuthMode.DeveloperCredential, Endpoint = ValidEndpoint };

        var availability = DocumentReadingAvailability.Resolve(options, isDevelopment: false);

        Assert.False(availability.IsAvailable);
        Assert.Contains("Development", availability.Reason);
    }

    [Theory]
    [InlineData(DocumentReadingAuthMode.AzureManagedIdentity, false)]
    [InlineData(DocumentReadingAuthMode.DeveloperCredential, true)]
    public void A_complete_configuration_makes_reading_available(DocumentReadingAuthMode auth, bool isDevelopment)
    {
        var options = new DocumentReadingOptions { Auth = auth, Endpoint = ValidEndpoint };

        var availability = DocumentReadingAvailability.Resolve(options, isDevelopment);

        Assert.True(availability.IsAvailable);
        Assert.NotNull(availability.Credential);
        Assert.Contains(ValidEndpoint.Host, availability.Reason);
    }

    [Fact]
    public void An_unknown_auth_value_makes_reading_unavailable_instead_of_throwing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentReading:Auth"] = "ApiKey",
                ["DocumentReading:Endpoint"] = ValidEndpoint.AbsoluteUri,
            })
            .Build();
        var environment = new HostingEnvironment { EnvironmentName = Environments.Production };

        var availability = DocumentReadingAvailability.Resolve(configuration, environment);

        Assert.False(availability.IsAvailable);
        Assert.False(availability.IsDisabled);
        Assert.DoesNotContain("ApiKey", availability.Reason);
    }
}
