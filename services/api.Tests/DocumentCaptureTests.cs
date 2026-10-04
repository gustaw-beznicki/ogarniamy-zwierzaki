using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ogarniamy_zwierzaki_api.Tests;

// The capture routes end to end on PostgreSQL and Azurite: manifest rules, file screening, completion and originals.
public sealed class DocumentCaptureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const long MaxFileBytes = 10_485_760;

    [Fact]
    public async Task Capture_defaults_list_the_owned_animals_and_the_default()
    {
        using var client = await factory.CreateCaptureClientAsync();

        var empty = await client.GetJsonAsync("/api/capture-defaults");
        Assert.Empty(empty.GetProperty("animals").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("defaultAnimalId").ValueKind);

        var first = await client.CreateAnimalIdAsync("Czarek");
        var second = await client.CreateAnimalIdAsync("Burek");
        var defaults = await client.GetJsonAsync("/api/capture-defaults");
        Assert.Equal([first, second], defaults.GetProperty("animals").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()));
        Assert.Equal(first, defaults.GetProperty("defaultAnimalId").GetGuid());

        await client.CaptureAsync(second, TestOriginal.Png());
        Assert.Equal(second, (await client.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());
    }

    [Fact]
    public async Task The_antiforgery_route_sets_an_http_only_same_site_cookie_and_is_not_cached()
    {
        var (client, _) = await factory.CreateSignedInClientAsync();
        using (client)
        {
            using var response = await client.GetAsync("/api/antiforgery");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("oz_csrf="));
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.False(string.IsNullOrEmpty((await response.ReadJsonAsync()).GetProperty("token").GetString()));
        }
    }

    [Fact]
    public async Task A_pdf_is_stored_and_its_original_is_returned_byte_for_byte_with_private_headers()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var pdf = TestOriginal.Pdf(300_000, "Vaccination 2024 — résumé.pdf");
        var operationId = Guid.NewGuid();

        using var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, pdf));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/document-uploads/{operationId}", created.Headers.Location?.OriginalString);
        var upload = await created.ReadJsonAsync();
        Assert.Equal("uploading", upload.GetProperty("state").GetString());
        Assert.Equal(DocumentApi.PastEventDate, upload.GetProperty("eventDate").GetString());

        using var stored = await client.PutFileAsync(operationId, 0, pdf);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        var receipt = await stored.ReadJsonAsync();
        Assert.True(receipt.GetProperty("stored").GetBoolean());
        Assert.Equal(pdf.Sha256, receipt.GetProperty("sha256").GetString());
        Assert.Equal(pdf.Bytes.LongLength, receipt.GetProperty("byteLength").GetInt64());

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var document = await completed.ReadJsonAsync();
        Assert.Equal(operationId, document.GetProperty("id").GetGuid());
        Assert.Equal(animalId, document.GetProperty("animalId").GetGuid());
        Assert.Equal("Czarek", document.GetProperty("animalName").GetString());
        Assert.Equal(DocumentApi.PastEventDate, document.GetProperty("eventDate").GetString());
        Assert.True(document.TryGetProperty("uploadedAt", out _));
        // No storage key or location: blob keys are JSON strings starting with "documents/".
        Assert.DoesNotContain("\"documents/", document.GetRawText());
        Assert.DoesNotContain("blob", document.GetRawText(), StringComparison.OrdinalIgnoreCase);

        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
        var detail = await client.GetJsonAsync($"/api/documents/{operationId}");
        Assert.Equal(document.GetRawText(), detail.GetRawText());

        // Served by the API alone: no content-processing (OCR or search) service exists or is needed.
        var originalUrl = Assert.Single(detail.OriginalUrls());
        using var original = await client.GetAsync(originalUrl);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal(pdf.Bytes, await original.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", original.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", original.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("Vaccination 2024 — résumé.pdf", original.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.True(original.Headers.CacheControl?.Private);
        Assert.True(original.Headers.CacheControl?.NoStore);
        Assert.Equal(["nosniff"], original.Headers.GetValues("X-Content-Type-Options"));
        Assert.Null(original.Headers.Location);

        using var download = await client.GetAsync($"{originalUrl}?download=true");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(pdf.Bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Ten_images_keep_their_confirmed_order_whatever_the_upload_order()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var images = Enumerable.Range(0, 10)
            .Select(i => i % 2 == 0 ? TestOriginal.Jpeg(1_000 + i, $"page-{i}.jpg") : TestOriginal.Png(1_000 + i, $"page-{i}.png"))
            .ToArray();
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, images)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        foreach (var position in Enumerable.Range(0, 10).Reverse())
        {
            using var uploaded = await client.PutFileAsync(operationId, position, images[position]);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        }

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var document = await completed.ReadJsonAsync();

        var files = document.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(0, 10), files.Select(f => f.GetProperty("position").GetInt32()));
        Assert.Equal(images.Select(i => i.Name), files.Select(f => f.GetProperty("originalName").GetString()));
        var urls = document.OriginalUrls();
        for (var position = 0; position < images.Length; position++)
        {
            using var original = await client.GetAsync(urls[position]);
            Assert.Equal(images[position].ContentType, original.Content.Headers.ContentType?.MediaType);
            Assert.Equal(images[position].Bytes, await original.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Well_formed_samples_and_a_pdf_with_more_pages_than_the_image_limit_are_stored_unchanged()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        // A PDF is one file whatever its internal page count: the ten-file limit counts images only.
        var pdf = SampleOriginals.Pdf(pages: 12);
        var photos = new[] { SampleOriginals.Jpeg("front.jpg"), SampleOriginals.Png("back.png") };

        var pdfDocument = await client.CaptureAsync(animalId, pdf);
        var photoDocument = await client.CaptureAsync(animalId, photos);

        var pdfFile = Assert.Single(pdfDocument.GetProperty("files").EnumerateArray());
        Assert.Equal("application/pdf", pdfFile.GetProperty("contentType").GetString());
        Assert.Equal(pdf.Bytes.LongLength, pdfFile.GetProperty("byteLength").GetInt64());
        Assert.Equal(pdf.Bytes, await client.GetOriginalBytesAsync(Assert.Single(pdfDocument.OriginalUrls())));

        var urls = photoDocument.OriginalUrls();
        Assert.Equal(2, urls.Length);
        Assert.Equal(photos[0].Bytes, await client.GetOriginalBytesAsync(urls[0]));
        Assert.Equal(photos[1].Bytes, await client.GetOriginalBytesAsync(urls[1]));
    }

    [Fact]
    public async Task The_capture_default_follows_the_account_to_another_device()
    {
        var (phone, email) = await factory.CreateSignedInClientAsync();
        using (phone)
        {
            await phone.UseAntiforgeryTokenAsync();
            var first = await phone.CreateAnimalIdAsync("Czarek");
            var second = await phone.CreateAnimalIdAsync("Burek");

            using var laptop = factory.CreateClient();
            using (var login = await laptop.LoginAsync(email, TestAccounts.ValidPassword))
            {
                Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            }

            Assert.Equal(first, (await laptop.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());

            // A completed capture on one device is the default on the other; a merely created operation is not.
            using (var pending = await phone.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(first, TestOriginal.Png())))
            {
                Assert.Equal(HttpStatusCode.Created, pending.StatusCode);
            }

            await phone.CaptureAsync(second, TestOriginal.Png());
            Assert.Equal(second, (await laptop.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());

            await laptop.UseAntiforgeryTokenAsync();
            await laptop.CaptureAsync(first, TestOriginal.Png());
            Assert.Equal(first, (await phone.GetJsonAsync("/api/capture-defaults")).GetProperty("defaultAnimalId").GetGuid());
        }
    }

    [Fact]
    public async Task Originals_support_byte_ranges()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var pdf = TestOriginal.Pdf(50_000);
        var document = await client.CaptureAsync(animalId, pdf);

        using var request = new HttpRequestMessage(HttpMethod.Get, Assert.Single(document.OriginalUrls()));
        request.Headers.Range = new RangeHeaderValue(100, 199);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(pdf.Bytes[100..200], await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Ten_mebibytes_are_accepted_and_one_byte_more_is_rejected()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();

        var tooLarge = TestOriginal.Pdf((int)MaxFileBytes + 1);
        using (var rejected = await client.PutManifestAsync(Guid.NewGuid(), DocumentApi.Manifest(animalId, tooLarge)))
        {
            await rejected.AssertProblemAsync(HttpStatusCode.RequestEntityTooLarge, "file_too_large");
        }

        var largest = TestOriginal.Pdf((int)MaxFileBytes);
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, largest)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        // A body over the limit is refused without being stored, even against a maximum-size slot.
        using (var oversized = await client.PutFileAsync(operationId, 0, tooLarge))
        {
            await oversized.AssertProblemAsync(HttpStatusCode.RequestEntityTooLarge, "file_too_large");
        }

        using (var incomplete = await client.CompleteAsync(operationId))
        {
            await incomplete.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
        }

        using (var stored = await client.PutFileAsync(operationId, 0, largest))
        {
            Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        }

        using var completed = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var url = Assert.Single((await completed.ReadJsonAsync()).OriginalUrls());
        Assert.Equal(largest.Bytes, await client.GetOriginalBytesAsync(url));
    }

    [Fact]
    public async Task A_document_is_one_pdf_or_one_to_ten_images()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var eleven = Enumerable.Range(0, 11).Select(_ => TestOriginal.Jpeg()).ToArray();

        await AssertManifestProblemAsync(client, DocumentApi.Manifest(animalId), HttpStatusCode.BadRequest, "invalid_file_set");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, eleven), HttpStatusCode.BadRequest, "invalid_file_set");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, TestOriginal.Pdf(), TestOriginal.Jpeg()),
            HttpStatusCode.BadRequest, "invalid_file_set");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, TestOriginal.Pdf(), TestOriginal.Pdf()),
            HttpStatusCode.BadRequest, "invalid_file_set");
        var photo = TestOriginal.Jpeg();
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, photo, TestOriginal.Png(), photo with { Name = "copy.jpg" }),
            HttpStatusCode.BadRequest, "duplicate_file");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, new TestOriginal("photo.heic", "image/heic", [1, 2, 3])),
            HttpStatusCode.UnsupportedMediaType, "unsupported_file_type");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, new TestOriginal("empty.png", "image/png", [])),
            HttpStatusCode.BadRequest, "empty_file");

        await client.CaptureAsync(animalId, TestOriginal.Pdf());
        await client.CaptureAsync(animalId, TestOriginal.Png());
        await client.CaptureAsync(animalId, eleven[..10]);
        Assert.Equal(3, (await client.ListDocumentIdsAsync(animalId)).Length);
    }

    [Fact]
    public async Task Manifest_fields_are_validated_explicitly()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();

        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(Guid.Empty, png), HttpStatusCode.BadRequest, "animal_required");
        foreach (var eventDate in new string?[] { null, "", "2024-02-30", "01.05.2024", "2024-05-01T00:00:00" })
        {
            await AssertManifestProblemAsync(
                client, DocumentApi.Manifest(animalId, eventDate, DocumentApi.TimeZone, png),
                HttpStatusCode.BadRequest, "invalid_event_date");
        }

        foreach (var timeZone in new string?[] { null, "", "Mars/Olympus_Mons", "../../etc/passwd", "Central European Standard Time" })
        {
            await AssertManifestProblemAsync(
                client, DocumentApi.Manifest(animalId, DocumentApi.PastEventDate, timeZone, png),
                HttpStatusCode.BadRequest, "invalid_time_zone");
        }

        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, png with { Name = new string('a', 256) }),
            HttpStatusCode.BadRequest, "invalid_file_name");
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, png with { Name = " \u200B\u0007 " }),
            HttpStatusCode.BadRequest, "invalid_file_name");

        var badHash = new { animalId, eventDate = DocumentApi.PastEventDate, timeZone = DocumentApi.TimeZone,
            files = new[] { new { name = "a.png", contentType = "image/png", byteLength = 10L, sha256 = "abc" } } };
        await AssertManifestProblemAsync(client, badHash, HttpStatusCode.BadRequest, "invalid_file_hash");

        // A foreign or unknown animal is not found.
        await AssertManifestProblemAsync(client, DocumentApi.Manifest(Guid.NewGuid(), png), HttpStatusCode.NotFound, null);
    }

    [Fact]
    public async Task File_names_are_stored_sanitized_and_255_characters_are_accepted()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var longest = TestOriginal.Png(name: new string('n', 251) + ".png");
        var disguised = TestOriginal.Jpeg(name: "C:\\fakepath\\..\\scan\u202Egnp.jpg\u0000 ");

        var document = await client.CaptureAsync(animalId, longest, disguised);

        var names = document.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("originalName").GetString());
        Assert.Equal([longest.Name, "scangnp.jpg"], names);
    }

    [Fact]
    public async Task Uploaded_bytes_must_match_the_manifest_and_a_supported_signature()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var jpeg = TestOriginal.Jpeg(2_000);
        var heic = new TestOriginal("photo.jpg", "image/jpeg", [.. "\0\0\0\u0018ftypheic"u8, .. new byte[100]]);
        var pngBytes = TestOriginal.Png(1_000);
        var pngDeclaredAsJpeg = pngBytes with { ContentType = "image/jpeg" };
        var operationId = Guid.NewGuid();
        using (var created = await client.PutManifestAsync(
                   operationId, DocumentApi.Manifest(animalId, jpeg, heic, pngDeclaredAsJpeg)))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var sameLengthOtherBytes = TestOriginal.Jpeg(2_000).Bytes;
        await AssertUploadProblemAsync(
            client.PutBytesAsync(operationId, 0, sameLengthOtherBytes), HttpStatusCode.BadRequest, "file_mismatch");
        await AssertUploadProblemAsync(
            client.PutBytesAsync(operationId, 0, jpeg.Bytes[..1_999]), HttpStatusCode.BadRequest, "file_mismatch");
        await AssertUploadProblemAsync(client.PutBytesAsync(operationId, 0, []), HttpStatusCode.BadRequest, "empty_file");
        await AssertUploadProblemAsync(
            client.PutFileAsync(operationId, 1, heic), HttpStatusCode.UnsupportedMediaType, "unsupported_file_type");
        await AssertUploadProblemAsync(
            client.PutFileAsync(operationId, 2, pngDeclaredAsJpeg), HttpStatusCode.BadRequest, "file_mismatch");

        using (var notMultipart = await client.PutAsync(
                   $"/api/document-uploads/{operationId}/files/0", new ByteArrayContent(jpeg.Bytes)))
        {
            await notMultipart.AssertProblemAsync(HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
        }

        using (var twoFiles = new MultipartFormDataContent())
        {
            twoFiles.Add(new ByteArrayContent(jpeg.Bytes), "file", "a.jpg");
            twoFiles.Add(new ByteArrayContent(jpeg.Bytes), "file", "b.jpg");
            await AssertUploadProblemAsync(
                client.PutAsync($"/api/document-uploads/{operationId}/files/0", twoFiles),
                HttpStatusCode.BadRequest, "invalid_upload_body");
        }

        using (var noFile = new MultipartFormDataContent())
        {
            noFile.Add(new StringContent("value"), "file");
            await AssertUploadProblemAsync(
                client.PutAsync($"/api/document-uploads/{operationId}/files/0", noFile),
                HttpStatusCode.BadRequest, "invalid_upload_body");
        }

        using (var missingSlot = await client.PutFileAsync(operationId, 3, jpeg))
        {
            Assert.Equal(HttpStatusCode.NotFound, missingSlot.StatusCode);
        }

        // Nothing was stored, so the operation cannot complete; the matching original still can be.
        using (var incomplete = await client.CompleteAsync(operationId))
        {
            await incomplete.AssertProblemAsync(HttpStatusCode.Conflict, "upload_incomplete");
        }

        using var stored = await client.PutFileAsync(operationId, 0, jpeg);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
    }

    [Fact]
    public async Task Event_dates_up_to_local_today_are_accepted_in_the_supplied_time_zone()
    {
        // 22:30 UTC on 4 October 2026: already 5 October in Warsaw (UTC+2), still 4 October in Los Angeles (UTC-7).
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 22, 30, 0, TimeSpan.Zero));
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));
        using var client = await host.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();

        await AssertCreatedAsync(client, DocumentApi.Manifest(animalId, "2026-10-05", "Europe/Warsaw", TestOriginal.Png()));
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, "2026-10-06", "Europe/Warsaw", TestOriginal.Png()),
            HttpStatusCode.BadRequest, "future_event_date");

        await AssertCreatedAsync(client, DocumentApi.Manifest(animalId, "2026-10-04", "America/Los_Angeles", TestOriginal.Png()));
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, "2026-10-05", "America/Los_Angeles", TestOriginal.Png()),
            HttpStatusCode.BadRequest, "future_event_date");

        await AssertCreatedAsync(client, DocumentApi.Manifest(animalId, "0001-01-01", "Pacific/Kiritimati", TestOriginal.Png()));

        // The date is checked when the operation is created, not again on a retry.
        var operationId = Guid.NewGuid();
        var manifest = DocumentApi.Manifest(animalId, "2026-10-05", "Europe/Warsaw", TestOriginal.Png());
        using (var created = await client.PutManifestAsync(operationId, manifest))
        {
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        clock.Now = clock.Now.AddDays(-2);
        using var retried = await client.PutManifestAsync(operationId, manifest);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    [Fact]
    public async Task Repeating_a_create_returns_the_same_operation_and_a_changed_manifest_conflicts()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var otherAnimalId = await client.CreateAnimalIdAsync("Burek");
        var jpeg = TestOriginal.Jpeg();
        var png = TestOriginal.Png();
        var operationId = Guid.NewGuid();

        using var created = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, jpeg, png));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var fileIds = (await created.ReadJsonAsync()).FileIds();

        using var repeated = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, jpeg, png));
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(fileIds, (await repeated.ReadJsonAsync()).FileIds());

        object[] changed =
        [
            DocumentApi.Manifest(otherAnimalId, jpeg, png),
            DocumentApi.Manifest(animalId, "2024-05-02", DocumentApi.TimeZone, jpeg, png),
            DocumentApi.Manifest(animalId, DocumentApi.PastEventDate, "Europe/Berlin", jpeg, png),
            DocumentApi.Manifest(animalId, png, jpeg),
            DocumentApi.Manifest(animalId, jpeg),
            DocumentApi.Manifest(animalId, jpeg, TestOriginal.Png()),
            DocumentApi.Manifest(animalId, jpeg, png with { Name = "renamed.png" }),
        ];
        foreach (var manifest in changed)
        {
            await AssertManifestProblemAsync(client, manifest, HttpStatusCode.Conflict, "upload_conflict", operationId);
        }

        // Completed operations still answer an identical retry, and still refuse a changed one.
        using (var first = await client.PutFileAsync(operationId, 0, jpeg))
        using (var second = await client.PutFileAsync(operationId, 1, png))
        using (var completed = await client.CompleteAsync(operationId))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }

        using var afterCompletion = await client.PutManifestAsync(operationId, DocumentApi.Manifest(animalId, jpeg, png));
        Assert.Equal(HttpStatusCode.OK, afterCompletion.StatusCode);
        Assert.Equal("stored", (await afterCompletion.ReadJsonAsync()).GetProperty("state").GetString());
        await AssertManifestProblemAsync(
            client, DocumentApi.Manifest(animalId, jpeg), HttpStatusCode.Conflict, "upload_conflict", operationId);
        Assert.Equal([operationId], await client.ListDocumentIdsAsync(animalId));
    }

    [Fact]
    public async Task A_completed_document_accepts_its_identical_bytes_again_and_rejects_changed_ones()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var png = TestOriginal.Png();
        var document = await client.CaptureAsync(animalId, png);
        var operationId = document.GetProperty("id").GetGuid();

        using (var same = await client.PutFileAsync(operationId, 0, png))
        {
            Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        }

        await AssertUploadProblemAsync(
            client.PutFileAsync(operationId, 0, TestOriginal.Png(png.Bytes.Length)), HttpStatusCode.BadRequest, "file_mismatch");

        using var repeated = await client.CompleteAsync(operationId);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(document.GetRawText(), (await repeated.ReadJsonAsync()).GetRawText());
        Assert.Equal(png.Bytes, await client.GetOriginalBytesAsync(Assert.Single(document.OriginalUrls())));
    }

    [Fact]
    public async Task Lists_page_by_offset_and_limit()
    {
        using var client = await factory.CreateCaptureClientAsync();
        var animalId = await client.CreateAnimalIdAsync();
        var first = await client.CaptureAsync(animalId, TestOriginal.Png());
        var second = await client.CaptureAsync(animalId, TestOriginal.Png());

        var page = await client.GetJsonAsync($"/api/animals/{animalId}/documents?offset=0&limit=1");
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        var item = Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal(second.GetProperty("id").GetGuid(), item.GetProperty("id").GetGuid());
        Assert.Equal(1, item.GetProperty("fileCount").GetInt32());
        Assert.Equal(DocumentApi.PastEventDate, item.GetProperty("eventDate").GetString());

        var last = await client.GetJsonAsync($"/api/animals/{animalId}/documents?offset=1&limit=1");
        Assert.False(last.GetProperty("hasMore").GetBoolean());
        Assert.Equal(first.GetProperty("id").GetGuid(), Assert.Single(last.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        foreach (var query in new[] { "limit=0", "limit=101", "offset=-1" })
        {
            using var invalid = await client.GetAsync($"/api/animals/{animalId}/documents?{query}");
            await invalid.AssertProblemAsync(HttpStatusCode.BadRequest, "invalid_pagination");
        }
    }

    [Fact]
    public async Task Manifests_must_be_json()
    {
        using var client = await factory.CreateCaptureClientAsync();

        using var response = await client.PutAsync(
            $"/api/document-uploads/{Guid.NewGuid()}", new StringContent("animalId=1", Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private static async Task AssertCreatedAsync(HttpClient client, object manifest)
    {
        using var response = await client.PutAsJsonAsync($"/api/document-uploads/{Guid.NewGuid()}", manifest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AssertManifestProblemAsync(
        HttpClient client, object manifest, HttpStatusCode status, string? code, Guid? operationId = null)
    {
        using var response = await client.PutManifestAsync(operationId ?? Guid.NewGuid(), manifest);
        if (code is null)
        {
            Assert.Equal(status, response.StatusCode);
            return;
        }

        await response.AssertProblemAsync(status, code);
    }

    private static async Task AssertUploadProblemAsync(Task<HttpResponseMessage> upload, HttpStatusCode status, string code)
    {
        using var response = await upload;
        await response.AssertProblemAsync(status, code);
    }
}
