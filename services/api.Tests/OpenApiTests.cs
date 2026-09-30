using System.Net;

namespace ogarniamy_zwierzaki_api.Tests;

// The test host runs in Development, where the OpenAPI document is mapped.
public sealed class OpenApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task OpenApi_document_is_served_without_signing_in()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
