using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ogarniamy_zwierzaki_api.Auth;

namespace ogarniamy_zwierzaki_api.Documents;

// Capture and document routes. Every route requires a session (fallback policy); every capture mutation also requires
// an antiforgery token in the X-CSRF-TOKEN header. Another account's animal, operation, document or file is
// indistinguishable from a missing one (404). No route exposes a storage endpoint or key.
public static class DocumentEndpoints
{
    // A manifest of ten files with 255-character names stays far below this.
    private const long ManifestRequestBytes = 64 * 1024;

    private const long EmptyRequestBytes = 1024;

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/capture-defaults", GetCaptureDefaultsAsync).WithName("GetCaptureDefaults");

        var uploads = app.MapGroup("/api/document-uploads/{operationId:guid}")
            .AddEndpointFilter<AntiforgeryHeaderFilter>();
        uploads.MapPut("/", CreateUploadAsync)
            .WithName("CreateDocumentUpload")
            .WithMetadata(new RequestSizeLimitAttribute(ManifestRequestBytes));
        uploads.MapPut("/files/{position:int}", UploadFileAsync)
            .WithName("UploadDocumentFile")
            .WithMetadata(new RequestSizeLimitAttribute(SingleFileMultipartReader.MaxRequestBytes));
        uploads.MapPost("/complete", CompleteUploadAsync)
            .WithName("CompleteDocumentUpload")
            .WithMetadata(new RequestSizeLimitAttribute(EmptyRequestBytes));

        app.MapGet("/api/animals/{animalId:guid}/documents", ListAsync).WithName("ListAnimalDocuments");
        app.MapGet("/api/documents/{documentId:guid}", GetAsync).WithName("GetDocument");
        app.MapGet("/api/documents/{documentId:guid}/files/{fileId:guid}/original", GetOriginalAsync)
            .WithName("GetDocumentOriginal");

        return app;
    }

    private static async Task<Ok<CaptureDefaults>> GetCaptureDefaultsAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, OwnedDocuments documents,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await documents.GetCaptureDefaultsAsync(CurrentUserId(principal, users), cancellationToken));

    private static async Task<Results<Created<DocumentUploadResponse>, Ok<DocumentUploadResponse>, NotFound, ProblemHttpResult>>
        CreateUploadAsync(
            Guid operationId, CreateDocumentUploadRequest request, ClaimsPrincipal principal, UserManager<AppUser> users,
            DocumentCaptureService capture, CancellationToken cancellationToken)
    {
        var result = await capture.CreateUploadAsync(CurrentUserId(principal, users), operationId, request, cancellationToken);
        if (result.Value is { } upload)
        {
            return result.Created
                ? TypedResults.Created($"/api/document-uploads/{operationId:D}", upload)
                : TypedResults.Ok(upload);
        }

        return result.Failure == DocumentCaptureFailure.NotFound ? TypedResults.NotFound() : ToProblem(result.Failure);
    }

    private static async Task<Results<Ok<DocumentUploadFileResponse>, NotFound, ProblemHttpResult>> UploadFileAsync(
        Guid operationId, int position, HttpRequest request, ClaimsPrincipal principal, UserManager<AppUser> users,
        DocumentCaptureService capture, CancellationToken cancellationToken)
    {
        var received = await SingleFileMultipartReader.ReadAsync(request, cancellationToken);
        if (received.Value is not { } content)
        {
            return ToProblem(received.Failure);
        }

        await using (content)
        {
            var result = await capture.UploadFileAsync(
                CurrentUserId(principal, users), operationId, position, content, cancellationToken);
            if (result.Value is { } file)
            {
                return TypedResults.Ok(file);
            }

            return result.Failure == DocumentCaptureFailure.NotFound ? TypedResults.NotFound() : ToProblem(result.Failure);
        }
    }

    private static async Task<Results<Ok<StoredDocumentResponse>, NotFound, ProblemHttpResult>> CompleteUploadAsync(
        Guid operationId, ClaimsPrincipal principal, UserManager<AppUser> users, DocumentCaptureService capture,
        CancellationToken cancellationToken)
    {
        var result = await capture.CompleteAsync(CurrentUserId(principal, users), operationId, cancellationToken);
        if (result.Value is { } document)
        {
            return TypedResults.Ok(document);
        }

        return result.Failure == DocumentCaptureFailure.NotFound ? TypedResults.NotFound() : ToProblem(result.Failure);
    }

    private static async Task<Results<Ok<DocumentPage>, NotFound, ProblemHttpResult>> ListAsync(
        Guid animalId, int? offset, int? limit, ClaimsPrincipal principal, UserManager<AppUser> users,
        OwnedDocuments documents, CancellationToken cancellationToken)
    {
        var pageOffset = offset ?? 0;
        var pageSize = limit ?? OwnedDocuments.DefaultPageSize;
        if (pageOffset < 0 || pageSize < 1 || pageSize > OwnedDocuments.MaxPageSize)
        {
            return ToProblem(DocumentCaptureFailure.InvalidPagination);
        }

        var page = await documents.ListStoredAsync(
            CurrentUserId(principal, users), animalId, pageOffset, pageSize, cancellationToken);
        return page is null ? TypedResults.NotFound() : TypedResults.Ok(page);
    }

    private static async Task<Results<Ok<StoredDocumentResponse>, NotFound>> GetAsync(
        Guid documentId, ClaimsPrincipal principal, UserManager<AppUser> users, OwnedDocuments documents,
        CancellationToken cancellationToken)
    {
        var document = await documents.FindStoredAsync(CurrentUserId(principal, users), documentId, cancellationToken);
        return document is null ? TypedResults.NotFound() : TypedResults.Ok(StoredDocumentResponse.From(document));
    }

    // Streams the original through the API after a fresh ownership check. The response is never cached, never
    // redirects to storage and supports byte ranges over the seekable storage stream, which is disposed with the
    // response.
    private static async Task<Results<FileStreamHttpResult, NotFound, ProblemHttpResult>> GetOriginalAsync(
        Guid documentId, Guid fileId, bool? download, HttpContext context, ClaimsPrincipal principal,
        UserManager<AppUser> users, DocumentCaptureService capture, CancellationToken cancellationToken)
    {
        var result = await capture.OpenOriginalAsync(
            CurrentUserId(principal, users), documentId, fileId, cancellationToken);
        if (result.Value is not { } original)
        {
            return result.Failure == DocumentCaptureFailure.NotFound ? TypedResults.NotFound() : ToProblem(result.Failure);
        }

        var disposition = new ContentDispositionHeaderValue(download == true ? "attachment" : "inline");
        // Quotes or encodes the sanitized name, so it cannot break out of the header.
        disposition.SetHttpFileName(original.File.OriginalName);

        var headers = context.Response.Headers;
        headers.ContentDisposition = disposition.ToString();
        headers.CacheControl = "private, no-store";
        headers.XContentTypeOptions = "nosniff";

        // The content type was validated against the original's signature when it was uploaded.
        return TypedResults.Stream(original.Content, original.File.ContentType, enableRangeProcessing: true);
    }

    private static ProblemHttpResult ToProblem(DocumentCaptureFailure? failure) => failure switch
    {
        DocumentCaptureFailure.AnimalRequired => Problem(StatusCodes.Status400BadRequest, "animal_required"),
        DocumentCaptureFailure.InvalidEventDate => Problem(StatusCodes.Status400BadRequest, "invalid_event_date"),
        DocumentCaptureFailure.FutureEventDate => Problem(StatusCodes.Status400BadRequest, "future_event_date"),
        DocumentCaptureFailure.InvalidTimeZone => Problem(StatusCodes.Status400BadRequest, "invalid_time_zone"),
        DocumentCaptureFailure.InvalidFileSet => Problem(StatusCodes.Status400BadRequest, "invalid_file_set"),
        DocumentCaptureFailure.DuplicateFile => Problem(StatusCodes.Status400BadRequest, "duplicate_file"),
        DocumentCaptureFailure.InvalidFileName => Problem(StatusCodes.Status400BadRequest, "invalid_file_name"),
        DocumentCaptureFailure.InvalidFileHash => Problem(StatusCodes.Status400BadRequest, "invalid_file_hash"),
        DocumentCaptureFailure.EmptyFile => Problem(StatusCodes.Status400BadRequest, "empty_file"),
        DocumentCaptureFailure.FileTooLarge => Problem(StatusCodes.Status413PayloadTooLarge, "file_too_large"),
        DocumentCaptureFailure.UnsupportedFileType =>
            Problem(StatusCodes.Status415UnsupportedMediaType, "unsupported_file_type"),
        DocumentCaptureFailure.UnsupportedMediaType =>
            Problem(StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type"),
        DocumentCaptureFailure.InvalidUploadBody => Problem(StatusCodes.Status400BadRequest, "invalid_upload_body"),
        DocumentCaptureFailure.FileMismatch => Problem(StatusCodes.Status400BadRequest, "file_mismatch"),
        DocumentCaptureFailure.UploadConflict => Problem(StatusCodes.Status409Conflict, "upload_conflict"),
        DocumentCaptureFailure.UploadIncomplete => Problem(StatusCodes.Status409Conflict, "upload_incomplete"),
        DocumentCaptureFailure.InvalidPagination => Problem(StatusCodes.Status400BadRequest, "invalid_pagination"),
        DocumentCaptureFailure.StorageUnavailable =>
            Problem(StatusCodes.Status503ServiceUnavailable, "storage_unavailable"),
        DocumentCaptureFailure.OriginalUnavailable =>
            Problem(StatusCodes.Status503ServiceUnavailable, "original_unavailable"),
        _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "No problem response for this failure."),
    };

    private static ProblemHttpResult Problem(int statusCode, string code) => ApiProblem.Create(statusCode, code);

    // The fallback policy guarantees an authenticated user, so a missing id is a server error.
    private static string CurrentUserId(ClaimsPrincipal principal, UserManager<AppUser> users) =>
        users.GetUserId(principal) ?? throw new InvalidOperationException("The authenticated user has no id claim.");
}
