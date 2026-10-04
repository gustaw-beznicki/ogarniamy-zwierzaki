using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Documents;

// The capture operation: a frozen manifest, one upload per original and a final completion, plus reading originals.
// There is no transaction across PostgreSQL and Blob Storage. The manifest row is written before any original;
// originals live under deterministic keys and are never overwritten, so an original stored before a failure (database
// error, timeout, lost response or cancellation) is found and its receipt recorded on the next retry of the same
// operation. A document becomes visible only through CompleteAsync, once every original has a matching receipt.
// Pending operations and their originals are kept; nothing here expires or deletes them.
//
// Logs carry operation, document and file IDs, states and failure codes; never file names or contents.
public sealed partial class DocumentCaptureService(
    OwnedDocuments documents,
    IOriginalStorage storage,
    TimeProvider clock,
    ILogger<DocumentCaptureService> logger)
{
    private const string EventDateFormat = "yyyy-MM-dd";

    // Creates the upload operation from its manifest. An identical retry returns the existing operation in any state;
    // a different manifest under the same operation ID is a conflict. The event date is compared with today in the
    // supplied time zone only when the operation is first created, never again on a retry.
    public async Task<CaptureResult<DocumentUploadResponse>> CreateUploadAsync(
        string userId, Guid operationId, CreateDocumentUploadRequest request, CancellationToken cancellationToken)
    {
        var parsed = ParseManifest(operationId, request);
        if (parsed.Value is not { } manifest)
        {
            return CaptureResult<DocumentUploadResponse>.Fail(parsed.Failure!.Value);
        }

        try
        {
            var existing = await documents.FindUploadAsync(userId, operationId, cancellationToken);
            if (existing is not null)
            {
                return Retried(existing, manifest);
            }

            if (manifest.EventDate > LocalToday(manifest.CaptureTimeZone))
            {
                return CaptureResult<DocumentUploadResponse>.Fail(DocumentCaptureFailure.FutureEventDate);
            }

            try
            {
                if (!await documents.AddUploadAsync(userId, manifest, cancellationToken))
                {
                    return CaptureResult<DocumentUploadResponse>.Fail(DocumentCaptureFailure.NotFound);
                }
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A concurrent create of the same operation, or an operation ID taken by another account.
                existing = await documents.FindUploadAsync(userId, operationId, cancellationToken);
                return existing is null
                    ? CaptureResult<DocumentUploadResponse>.Fail(DocumentCaptureFailure.NotFound)
                    : Retried(existing, manifest);
            }

            logger.LogInformation(
                "Upload operation {OperationId} created with {FileCount} file(s).", operationId, manifest.Files.Count);
            return CaptureResult<DocumentUploadResponse>.Ok(ToUploadResponse(manifest), created: true);
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            LogStorageUnavailable(exception, operationId, null);
            return CaptureResult<DocumentUploadResponse>.Fail(DocumentCaptureFailure.StorageUnavailable);
        }
    }

    // Stores one original of the operation and records its receipt. The bytes must match the manifest slot. A slot
    // that already has a receipt answers an identical retry without writing again. An original already in storage
    // (written by an earlier attempt whose receipt was not recorded) is reconciled when it matches the manifest and
    // reported as a conflict, never overwritten, when it does not.
    public async Task<CaptureResult<DocumentUploadFileResponse>> UploadFileAsync(
        string userId, Guid operationId, int position, MemoryStream content, CancellationToken cancellationToken)
    {
        try
        {
            var upload = await documents.FindUploadAsync(userId, operationId, cancellationToken);
            if (upload is null || position < 0 || position >= upload.Files.Count)
            {
                return CaptureResult<DocumentUploadFileResponse>.Fail(DocumentCaptureFailure.NotFound);
            }

            var slot = upload.Files[position];
            if (DocumentFileValidator.Verify(content.GetBuffer().AsSpan(0, (int)content.Length), slot) is { } failure)
            {
                logger.LogInformation(
                    "Upload of file {FileId} of operation {OperationId} rejected: {Failure}.",
                    slot.Id, operationId, failure);
                return CaptureResult<DocumentUploadFileResponse>.Fail(failure);
            }

            if (slot.Receipt is null)
            {
                content.Position = 0;
                var receipt = await storage.CreateIfAbsentAsync(slot.BlobKey, content, slot.ContentType, cancellationToken)
                    ?? await storage.GetReceiptAsync(slot.BlobKey, cancellationToken);
                if (receipt is null)
                {
                    // The key was taken a moment ago but its original cannot be found now.
                    LogStorageUnavailable(null, operationId, slot.Id);
                    return CaptureResult<DocumentUploadFileResponse>.Fail(DocumentCaptureFailure.StorageUnavailable);
                }

                if (!await TryRecordAsync(slot, receipt, operationId, cancellationToken))
                {
                    return CaptureResult<DocumentUploadFileResponse>.Fail(DocumentCaptureFailure.UploadConflict);
                }

                logger.LogInformation(
                    "File {FileId} at position {Position} of operation {OperationId} stored.",
                    slot.Id, slot.Position, operationId);
            }

            return CaptureResult<DocumentUploadFileResponse>.Ok(ToFileResponse(slot));
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            LogStorageUnavailable(exception, operationId, null);
            return CaptureResult<DocumentUploadFileResponse>.Fail(DocumentCaptureFailure.StorageUnavailable);
        }
    }

    // Completes the operation once every slot has a durable original with a matching receipt; receipts missing from
    // the database but present in storage are recorded first. The document becomes Stored exactly once; a repeated
    // completion returns the same document without further side effects.
    public async Task<CaptureResult<StoredDocumentResponse>> CompleteAsync(
        string userId, Guid operationId, CancellationToken cancellationToken)
    {
        try
        {
            var upload = await documents.FindUploadAsync(userId, operationId, cancellationToken);
            if (upload is null)
            {
                return CaptureResult<StoredDocumentResponse>.Fail(DocumentCaptureFailure.NotFound);
            }

            if (upload.StorageState == DocumentStorageState.Uploading)
            {
                var missing = 0;
                foreach (var slot in upload.Files.Where(f => f.Receipt is null))
                {
                    var receipt = await storage.GetReceiptAsync(slot.BlobKey, cancellationToken);
                    if (receipt is null)
                    {
                        missing++;
                    }
                    else if (!await TryRecordAsync(slot, receipt, operationId, cancellationToken))
                    {
                        return CaptureResult<StoredDocumentResponse>.Fail(DocumentCaptureFailure.UploadConflict);
                    }
                    else
                    {
                        logger.LogInformation(
                            "Receipt of file {FileId} of operation {OperationId} recovered from storage.",
                            slot.Id, operationId);
                    }
                }

                if (missing > 0)
                {
                    logger.LogInformation(
                        "Completion of operation {OperationId} refused: {MissingCount} original(s) missing.",
                        operationId, missing);
                    return CaptureResult<StoredDocumentResponse>.Fail(DocumentCaptureFailure.UploadIncomplete);
                }
            }

            var stored = await documents.CompleteAsync(userId, operationId, cancellationToken);
            if (stored is null)
            {
                return CaptureResult<StoredDocumentResponse>.Fail(DocumentCaptureFailure.NotFound);
            }

            logger.LogInformation("Upload operation {OperationId} is stored.", operationId);
            return CaptureResult<StoredDocumentResponse>.Ok(StoredDocumentResponse.From(stored));
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            LogStorageUnavailable(exception, operationId, null);
            return CaptureResult<StoredDocumentResponse>.Fail(DocumentCaptureFailure.StorageUnavailable);
        }
    }

    // Opens an original of the user's Stored document for streaming. A Stored row whose original cannot be found is
    // reported as unavailable and logged; the record is kept and nothing else is served in its place.
    public async Task<CaptureResult<OpenedOriginal>> OpenOriginalAsync(
        string userId, Guid documentId, Guid fileId, CancellationToken cancellationToken)
    {
        try
        {
            var file = await documents.FindStoredFileAsync(userId, documentId, fileId, cancellationToken);
            if (file is null)
            {
                return CaptureResult<OpenedOriginal>.Fail(DocumentCaptureFailure.NotFound);
            }

            var content = await storage.OpenReadAsync(file.BlobKey, cancellationToken);
            if (content is null)
            {
                logger.LogError(
                    "Original of file {FileId} of Stored document {DocumentId} is missing from storage.",
                    fileId, documentId);
                return CaptureResult<OpenedOriginal>.Fail(DocumentCaptureFailure.OriginalUnavailable);
            }

            return CaptureResult<OpenedOriginal>.Ok(new OpenedOriginal(file, content));
        }
        catch (Exception exception) when (TransientFailures.Is(exception, cancellationToken))
        {
            LogStorageUnavailable(exception, documentId, fileId);
            return CaptureResult<OpenedOriginal>.Fail(DocumentCaptureFailure.StorageUnavailable);
        }
    }

    // IANA IDs only: letters, digits and _+- in slash-separated segments, such as America/Argentina/Buenos_Aires.
    [GeneratedRegex("^[A-Za-z0-9_+-]+(/[A-Za-z0-9_+-]+)*$")]
    private static partial Regex TimeZoneIdPattern();

    private static CaptureResult<Document> ParseManifest(Guid operationId, CreateDocumentUploadRequest request)
    {
        if (request.AnimalId is not { } animalId || animalId == Guid.Empty)
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.AnimalRequired);
        }

        if (!DateOnly.TryParseExact(
                request.EventDate, EventDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate))
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidEventDate);
        }

        if (!IsSupportedTimeZone(request.TimeZone))
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidTimeZone);
        }

        if (request.Files is not { Count: > 0 } requested || requested.Any(f => f is null))
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidFileSet);
        }

        var contentTypes = requested.Select(f => DocumentFileValidator.NormalizeContentType(f!.ContentType)).ToList();
        if (contentTypes.Any(t => t is null))
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.UnsupportedFileType);
        }

        // One PDF, or 1-10 JPEG/PNG images.
        if (requested.Count > Document.MaxImageFiles
            || (requested.Count > 1 && contentTypes.Contains(DocumentFile.PdfContentType)))
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidFileSet);
        }

        var files = new List<DocumentFile>(requested.Count);
        for (var position = 0; position < requested.Count; position++)
        {
            var file = requested[position]!;
            if (DocumentFileValidator.SanitizeName(file.Name) is not { } name)
            {
                return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidFileName);
            }

            if (file.ByteLength is not { } byteLength || byteLength < 1)
            {
                return CaptureResult<Document>.Fail(DocumentCaptureFailure.EmptyFile);
            }

            if (byteLength > DocumentFile.MaxByteLength)
            {
                return CaptureResult<Document>.Fail(DocumentCaptureFailure.FileTooLarge);
            }

            if (DocumentFileValidator.NormalizeSha256(file.Sha256) is not { } sha256)
            {
                return CaptureResult<Document>.Fail(DocumentCaptureFailure.InvalidFileHash);
            }

            files.Add(new DocumentFile
            {
                OriginalName = name,
                ContentType = contentTypes[position]!,
                ByteLength = byteLength,
                Sha256 = sha256,
            });
        }

        // The same original twice in one document adds nothing; separate documents may still repeat a file.
        if (files.DistinctBy(f => f.Sha256).Count() != files.Count)
        {
            return CaptureResult<Document>.Fail(DocumentCaptureFailure.DuplicateFile);
        }

        return CaptureResult<Document>.Ok(new Document
        {
            Id = operationId,
            AnimalId = animalId,
            EventDate = eventDate,
            CaptureTimeZone = request.TimeZone!,
            Files = files,
        });
    }

    private static bool IsSupportedTimeZone(string? timeZone) =>
        timeZone is { Length: > 0 and <= Document.TimeZoneMaxLength }
        && TimeZoneIdPattern().IsMatch(timeZone)
        && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone)
        && zone.HasIanaId;

    // An identical manifest has the same animal, event date, time zone and ordered files.
    private static bool SameManifest(Document existing, Document requested) =>
        existing.AnimalId == requested.AnimalId
        && existing.EventDate == requested.EventDate
        && existing.CaptureTimeZone == requested.CaptureTimeZone
        && existing.Files.Count == requested.Files.Count
        && existing.Files.Zip(requested.Files).All(pair =>
            pair.First.OriginalName == pair.Second.OriginalName
            && pair.First.ContentType == pair.Second.ContentType
            && pair.First.ByteLength == pair.Second.ByteLength
            && pair.First.Sha256 == pair.Second.Sha256);

    private static DocumentUploadResponse ToUploadResponse(Document document) => new(
        document.Id,
        document.AnimalId,
        document.EventDate,
        document.CaptureTimeZone,
        document.StorageState == DocumentStorageState.Stored ? "stored" : "uploading",
        document.Files.OrderBy(f => f.Position).Select(ToFileResponse).ToList());

    private static DocumentUploadFileResponse ToFileResponse(DocumentFile file) => new(
        file.Position, file.Id, file.OriginalName, file.ContentType, file.ByteLength, file.Sha256, file.IsStored);

    private CaptureResult<DocumentUploadResponse> Retried(Document existing, Document requested)
    {
        if (SameManifest(existing, requested))
        {
            return CaptureResult<DocumentUploadResponse>.Ok(ToUploadResponse(existing));
        }

        logger.LogInformation("Upload operation {OperationId} retried with a different manifest.", existing.Id);
        return CaptureResult<DocumentUploadResponse>.Fail(DocumentCaptureFailure.UploadConflict);
    }

    private DateOnly LocalToday(string timeZone) =>
        DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);

    // Records a storage receipt for the slot; false, after logging, when it differs from the manifest or from an
    // already recorded receipt. The stored original is left as it is in both cases.
    private async Task<bool> TryRecordAsync(
        DocumentFile slot, OriginalFileReceipt receipt, Guid operationId, CancellationToken cancellationToken)
    {
        if (receipt.Length == slot.ByteLength
            && receipt.Sha256 == slot.Sha256
            && await documents.RecordReceiptAsync(slot, receipt, cancellationToken))
        {
            return true;
        }

        logger.LogWarning(
            "File {FileId} of operation {OperationId} has a different original in storage: {Failure}.",
            slot.Id, operationId, DocumentCaptureFailure.UploadConflict);
        return false;
    }

    private void LogStorageUnavailable(Exception? exception, Guid id, Guid? fileId) =>
        logger.LogWarning(
            exception,
            "Storage unavailable for operation/document {OperationId}, file {FileId}: {Failure}.",
            id, fileId, DocumentCaptureFailure.StorageUnavailable);
}
