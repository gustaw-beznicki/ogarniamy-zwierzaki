using System.Net;
using System.Net.Http.Json;

namespace ogarniamy_zwierzaki_api.Tests;

// FR-002 for documents: no session gets 401, another account's resources are indistinguishable from missing ones (404),
// pending operations are invisible, and capture mutations need a valid antiforgery token before anything changes.
public sealed class DocumentIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Requests_without_a_session_get_401()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var document = await owner.CaptureAsync(animalId, TestOriginal.Png());
        var documentId = document.GetProperty("id").GetGuid();
        var originalUrl = Assert.Single(document.OriginalUrls());
        // No redirects are followed, so a redirect to a sign-in page would fail the assertions.
        using var anonymous = TestAccounts.CreateClient(factory);
        var token = await owner.GetAntiforgeryTokenAsync();
        anonymous.DefaultRequestHeaders.Add(TestAccounts.AntiforgeryHeader, token);

        foreach (var path in new[]
                 {
                     "/api/capture-defaults", "/api/antiforgery", $"/api/animals/{animalId}/documents",
                     $"/api/documents/{documentId}", originalUrl, $"{originalUrl}?download=true",
                 })
        {
            using var response = await anonymous.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Null(response.Headers.Location);
        }

        var operationId = Guid.NewGuid();
        using (var create = await anonymous.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, TestOriginal.Png())))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        }

        using (var upload = await anonymous.PutFileAsync(documentId, 0, TestOriginal.Png()))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        }

        using (var complete = await anonymous.CompleteAsync(documentId))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, complete.StatusCode);
        }

        Assert.Equal([documentId], await owner.ListDocumentIdsAsync(animalId));
    }

    [Fact]
    public async Task Another_accounts_documents_animals_and_operations_are_not_found()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var otherAnimalId = await other.CreateAnimalIdAsync("Burek");
        var png = TestOriginal.Png();
        var document = await owner.CaptureAsync(animalId, png);
        var documentId = document.GetProperty("id").GetGuid();
        var fileId = document.GetProperty("files")[0].GetProperty("id").GetGuid();
        var pending = TestOriginal.Jpeg();
        var pendingId = Guid.NewGuid();
        using (var created = await owner.PutManifestAsync(pendingId, DocumentApi.Manifest(animalId, pending)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        foreach (var path in new[]
                 {
                     $"/api/animals/{animalId}/documents", $"/api/documents/{documentId}",
                     $"/api/documents/{documentId}/files/{fileId}/original",
                     $"/api/documents/{documentId}/files/{fileId}/original?download=true",
                     $"/api/documents/{pendingId}",
                 })
        {
            using var response = await other.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // The other account cannot create under the owner's animal, reuse the owner's operation IDs, upload into
        // them or complete them.
        await AssertNotFoundAsync(other.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, png)));
        await AssertNotFoundAsync(other.PutManifestAsync(pendingId, DocumentApi.Manifest(animalId, pending)));
        await AssertNotFoundAsync(other.PutManifestAsync(pendingId, DocumentApi.Manifest(otherAnimalId, pending)));
        await AssertNotFoundAsync(other.PutManifestAsync(documentId, DocumentApi.Manifest(otherAnimalId, png)));
        await AssertNotFoundAsync(other.PutFileAsync(pendingId, 0, pending));
        await AssertNotFoundAsync(other.PutFileAsync(documentId, 0, png));
        await AssertNotFoundAsync(other.CompleteAsync(pendingId));
        await AssertNotFoundAsync(other.CompleteAsync(documentId));

        // Nothing changed for either account.
        Assert.Equal([documentId], await owner.ListDocumentIdsAsync(animalId));
        Assert.Empty(await other.ListDocumentIdsAsync(otherAnimalId));
        Assert.Equal(otherAnimalId, (await other.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());
        using var stillPending = await owner.CompleteAsync(pendingId);
        await stillPending.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
    }

    [Fact]
    public async Task Another_account_cannot_reach_any_original_of_a_pdf_or_a_ten_image_document()
    {
        using var owner = await factory.CreateCaptureClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        var animalId = await owner.CreateAnimalIdAsync();
        var otherAnimalId = await other.CreateAnimalIdAsync("Burek");
        var pdf = SampleOriginals.Pdf(pages: 3);
        var images = Enumerable.Range(0, 10)
            .Select(i => i % 2 == 0 ? TestOriginal.Jpeg(900 + i, $"page-{i}.jpg") : TestOriginal.Png(900 + i, $"page-{i}.png"))
            .ToArray();
        var pdfDocument = await owner.CaptureAsync(animalId, pdf);
        var imageDocument = await owner.CaptureAsync(animalId, images);
        var otherDocument = await other.CaptureAsync(otherAnimalId, TestOriginal.Png());
        var ownerUrls = pdfDocument.OriginalUrls().Concat(imageDocument.OriginalUrls()).ToArray();
        Assert.Equal(11, ownerUrls.Length);

        foreach (var url in ownerUrls.Concat(ownerUrls.Select(u => $"{u}?download=true")))
        {
            using var response = await other.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }

        // A file ID is reachable only through its own document, even when the caller owns the other document.
        var otherDocumentId = otherDocument.GetProperty("id").GetGuid();
        var otherFileId = otherDocument.GetProperty("files")[0].GetProperty("id").GetGuid();
        var ownerFileId = imageDocument.GetProperty("files")[0].GetProperty("id").GetGuid();
        await AssertNotFoundAsync(other.GetAsync($"/api/documents/{otherDocumentId}/files/{ownerFileId}/original"));
        await AssertNotFoundAsync(owner.GetAsync($"/api/documents/{imageDocument.GetProperty("id").GetGuid()}/files/{otherFileId}/original"));
        await AssertNotFoundAsync(owner.GetAsync(
            $"/api/documents/{pdfDocument.GetProperty("id").GetGuid()}/files/{ownerFileId}/original"));

        // Every slot of a pending ten-image operation is closed to the other account, and so is its completion.
        var pendingId = Guid.NewGuid();
        using (var created = await owner.PutManifestAsync(pendingId, DocumentApi.Manifest(animalId, images)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        for (var position = 0; position < images.Length; position++)
        {
            await AssertNotFoundAsync(other.PutFileAsync(pendingId, position, images[position]));
        }

        await AssertNotFoundAsync(other.CompleteAsync(pendingId));
        await AssertNotFoundAsync(other.GetAsync($"/api/animals/{animalId}/documents"));
        using (var incomplete = await owner.CompleteAsync(pendingId))
        {
            await incomplete.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
        }

        // The owner still reads every original in its confirmed order; the other account sees only its own document.
        Assert.Equal(pdf.Bytes, await owner.GetOriginalBytesAsync(ownerUrls[0]));
        for (var position = 0; position < images.Length; position++)
        {
            Assert.Equal(images[position].Bytes, await owner.GetOriginalBytesAsync(ownerUrls[position + 1]));
        }

        Assert.Equal(
            [imageDocument.GetProperty("id").GetGuid(), pdfDocument.GetProperty("id").GetGuid()],
            await owner.ListDocumentIdsAsync(animalId));
        Assert.Equal([otherDocumentId], await other.ListDocumentIdsAsync(otherAnimalId));
    }

    [Fact]
    public async Task Pending_operations_are_invisible_until_completed()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var jpeg = TestOriginal.Jpeg();
        var png = TestOriginal.Png();
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, jpeg, png)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using (var first = await client.PutFileAsync(operationId, 0, jpeg))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var partial = await client.CompleteAsync(operationId))
        {
            await partial.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
        }

        Assert.Empty(await client.ListDocumentIdsAsync(animalId));
        using (var detail = await client.GetAsync($"/api/documents/{operationId}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        }

        using (var second = await client.PutFileAsync(operationId, 1, png))
        {
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
    }

    [Fact]
    public async Task Capture_mutations_without_a_valid_antiforgery_token_change_nothing()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using var other = await factory.CreateCaptureClientAsync();
        using (client)
        {
            var animalId = await client.CreateAnimalIdAsync();
            var png = TestOriginal.Png();
            var operationId = Guid.NewGuid();
            var manifest = DocumentApi.Manifest(animalId, png);
            var otherAccountsToken = await other.GetAntiforgeryTokenAsync();

            // No token, a forged token, and another account's token, before the client has an antiforgery cookie
            // and after.
            foreach (var token in new string?[] { null, "forged", otherAccountsToken })
            {
                await AssertRejectedAsync(client, token, c => c.PutManifestAsync(operationId, manifest));
            }

            var validToken = await client.GetAntiforgeryTokenAsync();
            foreach (var token in new string?[] { null, "", "forged", otherAccountsToken })
            {
                await AssertRejectedAsync(client, token, c => c.PutManifestAsync(operationId, manifest));
            }

            // The operation was never created, so a valid create is the first one.
            using (var created = await SendWithTokenAsync(client, validToken, c => c.PutManifestAsync(operationId, manifest)))
            {
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            }

            foreach (var token in new string?[] { null, "forged", otherAccountsToken })
            {
                await AssertRejectedAsync(client, token, c => c.PutFileAsync(operationId, 0, png));
            }

            // The original was not stored, and completion is refused without a token too.
            foreach (var token in new string?[] { null, "forged", otherAccountsToken })
            {
                await AssertRejectedAsync(client, token, c => c.CompleteAsync(operationId));
            }

            using (var incomplete = await SendWithTokenAsync(client, validToken, c => c.CompleteAsync(operationId)))
            {
                await incomplete.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
            }

            Assert.Empty(await client.ListDocumentIdsAsync(animalId));
        }
    }

    [Fact]
    public async Task Tokens_are_bound_to_the_signed_in_account()
    {
        var (client, email) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            var animalId = await client.CreateAnimalIdAsync();
            var staleToken = await client.GetAntiforgeryTokenAsync();
            using (var logout = await client.PostAsync("/api/auth/logout", null))
            {
                Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            }

            using var other = await factory.CreateCaptureClientAsync();
            using (var login = await client.LoginAsync(email, TestAccounts.ValidPassword))
            {
                Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            }

            // Signing in again as the same account keeps its token valid; another account's token is refused.
            using (var sameAccount = await SendWithTokenAsync(
                       client, staleToken, c => c.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png()))))
            {
                Assert.Equal(HttpStatusCode.Created, sameAccount.StatusCode);
            }

            await AssertRejectedAsync(
                client, await other.GetAntiforgeryTokenAsync(),
                c => c.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, TestOriginal.Png())));
        }
    }

    private static async Task AssertNotFoundAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task AssertRejectedAsync(
        HttpClient client, string? token, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        using var response = await SendWithTokenAsync(client, token, send);
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_antiforgery_token");
    }

    private static async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpClient client, string? token, Func<HttpClient, Task<HttpResponseMessage>> send)
    {
        client.DefaultRequestHeaders.Remove(TestAccounts.AntiforgeryHeader);
        if (token is not null)
        {
            client.DefaultRequestHeaders.Add(TestAccounts.AntiforgeryHeader, token);
        }

        try
        {
            return await send(client);
        }
        finally
        {
            client.DefaultRequestHeaders.Remove(TestAccounts.AntiforgeryHeader);
        }
    }
}
