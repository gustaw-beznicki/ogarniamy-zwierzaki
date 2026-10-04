using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ogarniamy_zwierzaki_api.Tests;

// HTTP helpers for the capture and document routes, used the way the browser uses them.
public static class DocumentApi
{
    // A past date in every time zone, whatever the real clock says.
    public const string PastEventDate = "2024-05-01";

    public const string TimeZone = "Europe/Warsaw";

    // A signed-in client that sends a valid antiforgery token with every request.
    public static async Task<HttpClient> CreateCaptureClientAsync(this WebApplicationFactory<Program> factory)
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        await client.UseAntiforgeryTokenAsync();
        return client;
    }

    public static async Task<Guid> CreateAnimalIdAsync(this HttpClient client, string name = "Czarek")
    {
        using var response = await client.CreateAnimalAsync(name);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static object Manifest(Guid animalId, params TestOriginal[] files) =>
        Manifest(animalId, PastEventDate, TimeZone, files);

    public static object Manifest(Guid animalId, string? eventDate, string? timeZone, params TestOriginal[] files) =>
        new { animalId, eventDate, timeZone, files = files.Select(f => f.ToManifestFile()).ToArray() };

    public static Task<HttpResponseMessage> PutManifestAsync(this HttpClient client, Guid operationId, object manifest) =>
        client.PutAsJsonAsync($"/api/document-uploads/{operationId}", manifest);

    public static Task<HttpResponseMessage> PutFileAsync(
        this HttpClient client, Guid operationId, int position, TestOriginal file) =>
        client.PutBytesAsync(operationId, position, file.Bytes, file.Name, file.ContentType);

    // One multipart file part named "file", as a browser FormData upload sends it.
    public static async Task<HttpResponseMessage> PutBytesAsync(
        this HttpClient client, Guid operationId, int position, byte[] bytes, string name = "upload",
        string contentType = "application/octet-stream")
    {
        using var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(part, "file", name);
        return await client.PutAsync($"/api/document-uploads/{operationId}/files/{position}", content);
    }

    public static Task<HttpResponseMessage> CompleteAsync(this HttpClient client, Guid operationId) =>
        client.PostAsync($"/api/document-uploads/{operationId}/complete", null);

    // Creates the operation, uploads every original in order and completes it; returns the Stored document.
    public static async Task<JsonElement> CaptureAsync(this HttpClient client, Guid animalId, params TestOriginal[] files)
    {
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, Manifest(animalId, files)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        for (var position = 0; position < files.Length; position++)
        {
            using var uploaded = await client.PutFileAsync(operationId, position, files[position]);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        return await completed.ReadJsonAsync();
    }

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static async Task<Guid[]> ListDocumentIdsAsync(this HttpClient client, Guid animalId) =>
        (await client.GetJsonAsync($"/api/animals/{animalId}/documents")).GetProperty("items").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid())
            .ToArray();

    public static async Task<byte[]> GetOriginalBytesAsync(this HttpClient client, string originalUrl)
    {
        using var response = await client.GetAsync(originalUrl);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public static string[] OriginalUrls(this JsonElement document) =>
        document.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("originalUrl").GetString() ?? string.Empty)
            .ToArray();

    public static Guid[] FileIds(this JsonElement upload) =>
        upload.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("fileId").GetGuid()).ToArray();
}
