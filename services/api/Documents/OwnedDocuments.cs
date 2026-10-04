using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ogarniamy_zwierzaki_api.Animals;
using ogarniamy_zwierzaki_api.Data;
using ogarniamy_zwierzaki_api.Storage;

namespace ogarniamy_zwierzaki_api.Documents;

// The only code allowed to read or write documents. Every operation is scoped to the animals the user owns
// (animal_members rows with the owner role); a document has no owner of its own. Another account's document or
// animal is indistinguishable from a missing one: both yield null or false.
//
// Uploading documents are reachable only through the upload-operation methods (AddUploadAsync, FindUploadAsync,
// RecordReceiptAsync, CompleteAsync); lists and Stored reads never return them.
public sealed class OwnedDocuments(AppDbContext db, OwnedAnimals animals)
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 100;

    // The capture animals and the account's default. The saved preference counts only while the animal is still
    // one of the user's; otherwise the earliest-created animal (then lowest ID) is the default, as in the animal list.
    public async Task<CaptureDefaults> GetCaptureDefaultsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var owned = await animals.ListAsync(userId, cancellationToken);
        var saved = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.LastCaptureAnimalId)
            .FirstOrDefaultAsync(cancellationToken);

        var defaultAnimalId = saved is { } id && owned.Any(a => a.Id == id) ? id : owned.FirstOrDefault()?.Id;
        return new CaptureDefaults(owned, defaultAnimalId);
    }

    // A page of the animal's Stored documents: EventDate, then UploadedAt, then ID, all descending.
    // Null when the animal does not exist or is not the user's.
    public async Task<DocumentPage?> ListStoredAsync(
        string userId, Guid animalId, int offset, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxPageSize);

        if (!await IsOwnedAnimalAsync(userId, animalId, cancellationToken))
        {
            return null;
        }

        // One extra row tells whether another page exists.
        var rows = await StoredForUser(userId)
            .Where(d => d.AnimalId == animalId)
            .OrderByDescending(d => d.EventDate)
            .ThenByDescending(d => d.UploadedAt)
            .ThenByDescending(d => d.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(d => new DocumentSummary(d.Id, d.AnimalId, d.EventDate, d.UploadedAt!.Value, d.Files.Count))
            .ToListAsync(cancellationToken);

        return rows.Count > limit ? new DocumentPage(rows[..limit], true) : new DocumentPage(rows, false);
    }

    // A Stored document with its originals in order. Null when it is missing, foreign or still Uploading.
    public Task<DocumentDetails?> FindStoredAsync(string userId, Guid documentId, CancellationToken cancellationToken = default) =>
        StoredForUser(userId)
            .Where(d => d.Id == documentId)
            .Select(d => new DocumentDetails(
                d.Id,
                d.AnimalId,
                db.Animals.Where(a => a.Id == d.AnimalId).Select(a => a.Name).First(),
                d.EventDate,
                d.UploadedAt!.Value,
                d.Files
                    .OrderBy(f => f.Position)
                    .Select(f => new DocumentFileDetails(f.Id, f.Position, f.OriginalName, f.ContentType, f.ByteLength, f.BlobKey))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    // One original of a Stored document. Null when the document or file is missing, foreign or still Uploading.
    public Task<DocumentFileDetails?> FindStoredFileAsync(
        string userId, Guid documentId, Guid fileId, CancellationToken cancellationToken = default) =>
        StoredForUser(userId)
            .Where(d => d.Id == documentId)
            .SelectMany(d => d.Files)
            .Where(f => f.Id == fileId)
            .Select(f => new DocumentFileDetails(f.Id, f.Position, f.OriginalName, f.ContentType, f.ByteLength, f.BlobKey))
            .FirstOrDefaultAsync(cancellationToken);

    // Persists a new, frozen manifest as an Uploading document. The caller supplies the operation ID (Document.Id),
    // AnimalId, EventDate, CaptureTimeZone and the files' manifest fields in order; this method assigns file IDs,
    // positions, blob keys, the state and CreatedAt. Returns false, writing nothing, when the animal is not the user's.
    // The file set must already be validated: an invalid one throws ArgumentException. An existing operation ID
    // (the user's own or another account's) makes SaveChanges throw DbUpdateException; the caller then resolves the
    // operation through FindUploadAsync, which returns null for a foreign one.
    public async Task<bool> AddUploadAsync(string userId, Document document, CancellationToken cancellationToken = default)
    {
        EnsureValidFileSet(document.Files);
        if (!await IsOwnedAnimalAsync(userId, document.AnimalId, cancellationToken))
        {
            return false;
        }

        document.StorageState = DocumentStorageState.Uploading;
        document.CreatedAt = DateTimeOffset.UtcNow;
        document.UploadedAt = null;
        for (var position = 0; position < document.Files.Count; position++)
        {
            var file = document.Files[position];
            file.Id = file.Id == Guid.Empty ? Guid.CreateVersion7() : file.Id;
            file.DocumentId = document.Id;
            file.Position = position;
            file.BlobKey = DocumentFile.BlobKeyFor(document.Id, file.Id);
            file.ReceiptLength = null;
            file.ReceiptSha256 = null;
            file.ReceiptEtag = null;
        }

        db.Documents.Add(document);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Leave the context clean, so the caller can look the existing operation up with the same context.
            db.ChangeTracker.Clear();
            throw;
        }

        return true;
    }

    // The user's upload operation in any state, tracked, with files in position order.
    // Null when the operation does not exist or belongs to another account's animal.
    public Task<Document?> FindUploadAsync(string userId, Guid operationId, CancellationToken cancellationToken = default) =>
        ForUser(userId)
            .Where(d => d.Id == operationId)
            .Include(d => d.Files.OrderBy(f => f.Position))
            .FirstOrDefaultAsync(cancellationToken);

    // Records the durable storage receipt of a file obtained from FindUploadAsync. The receipt must match the
    // manifest's length and SHA-256 (the caller checks this first; a mismatch throws ArgumentException). Returns true
    // when the file's recorded receipt now equals this one, including a repeated call, and false when a different
    // receipt was already recorded; an existing receipt is never replaced.
    public async Task<bool> RecordReceiptAsync(
        DocumentFile file, OriginalFileReceipt receipt, CancellationToken cancellationToken = default)
    {
        if (receipt.Length != file.ByteLength || receipt.Sha256 != file.Sha256)
        {
            throw new ArgumentException("The receipt does not match the file's manifest.", nameof(receipt));
        }

        // Conditional, so concurrent uploads of the same slot cannot overwrite each other's receipt.
        await db.DocumentFiles
            .Where(f => f.Id == file.Id && f.ReceiptEtag == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(f => f.ReceiptLength, receipt.Length)
                    .SetProperty(f => f.ReceiptSha256, receipt.Sha256)
                    .SetProperty(f => f.ReceiptEtag, receipt.ETag),
                cancellationToken);

        var recorded = await db.DocumentFiles
            .AsNoTracking()
            .Where(f => f.Id == file.Id)
            .Select(f => new { f.ReceiptLength, f.ReceiptSha256, f.ReceiptEtag })
            .SingleAsync(cancellationToken);

        // Keep the tracked entity in step with the database without marking it modified.
        var entry = db.Entry(file);
        SetUnmodified(entry.Property(f => f.ReceiptLength), recorded.ReceiptLength);
        SetUnmodified(entry.Property(f => f.ReceiptSha256), recorded.ReceiptSha256);
        SetUnmodified(entry.Property(f => f.ReceiptEtag), recorded.ReceiptEtag);

        return recorded.ReceiptEtag == receipt.ETag
            && recorded.ReceiptLength == receipt.Length
            && recorded.ReceiptSha256 == receipt.Sha256;
    }

    // Completes the user's upload operation: in one transaction the document becomes Stored with UploadedAt and the
    // account's LastCaptureAnimalId becomes its animal. An already Stored document is returned unchanged, without
    // touching UploadedAt or the preference again. Null when the operation is missing or foreign. Every file must
    // already have a receipt (the caller checks this and answers upload_incomplete); otherwise this throws
    // InvalidOperationException and changes nothing.
    public async Task<DocumentDetails?> CompleteAsync(
        string userId, Guid operationId, CancellationToken cancellationToken = default)
    {
        if (!await ForUser(userId).AnyAsync(d => d.Id == operationId, cancellationToken))
        {
            return null;
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Lock order: document, then account. Concurrent completions of one document serialize on the document
            // row; completions of different documents serialize on the account row, so the last one to commit
            // determines the preference.
            await db.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM documents WHERE id = {operationId} FOR UPDATE", cancellationToken);

            // Read after the lock, so a completion committed meanwhile is seen.
            var current = await db.Documents
                .AsNoTracking()
                .Where(d => d.Id == operationId)
                .Select(d => new
                {
                    d.AnimalId,
                    d.StorageState,
                    FileCount = d.Files.Count,
                    MissingReceipts = d.Files.Count(f => f.ReceiptEtag == null),
                })
                .SingleAsync(cancellationToken);

            if (current.StorageState == DocumentStorageState.Uploading)
            {
                if (current.FileCount == 0 || current.MissingReceipts > 0)
                {
                    throw new InvalidOperationException(
                        $"Document {operationId} cannot be completed: not every file has a storage receipt.");
                }

                await db.Database.ExecuteSqlAsync(
                    $"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE", cancellationToken);

                var now = DateTimeOffset.UtcNow;
                await db.Documents
                    .Where(d => d.Id == operationId && d.StorageState == DocumentStorageState.Uploading)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(d => d.StorageState, DocumentStorageState.Stored)
                            .SetProperty(d => d.UploadedAt, now),
                        cancellationToken);
                await db.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(u => u.LastCaptureAnimalId, current.AnimalId),
                        cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return await FindStoredAsync(userId, operationId, cancellationToken);
    }

    // Cross-row parts of the mode rule, which the database checks only per row: exactly one PDF, or 1-10 images
    // that are all JPEG/PNG. Per-file limits (length, hash, name) are also checked by the database.
    private static void EnsureValidFileSet(IReadOnlyList<DocumentFile> files)
    {
        if (files.Count == 0)
        {
            throw new ArgumentException("A document needs at least one file.", nameof(files));
        }

        if (files.Any(f => !DocumentFile.IsAllowedContentType(f.ContentType)))
        {
            throw new ArgumentException("A document file has an unsupported content type.", nameof(files));
        }

        var pdfCount = files.Count(f => f.ContentType == DocumentFile.PdfContentType);
        if (pdfCount > 0 && files.Count != 1)
        {
            throw new ArgumentException("A PDF document has exactly one file and no images.", nameof(files));
        }

        if (files.Count > Document.MaxImageFiles)
        {
            throw new ArgumentException($"A document has at most {Document.MaxImageFiles} images.", nameof(files));
        }
    }

    private static void SetUnmodified<T>(PropertyEntry<DocumentFile, T> property, T value)
    {
        property.OriginalValue = value;
        property.CurrentValue = value;
    }

    private Task<bool> IsOwnedAnimalAsync(string userId, Guid animalId, CancellationToken cancellationToken) =>
        OwnerMemberships(userId).AnyAsync(m => m.AnimalId == animalId, cancellationToken);

    private IQueryable<AnimalMember> OwnerMemberships(string userId) =>
        db.AnimalMembers.Where(m => m.UserId == userId && m.Role == AnimalRole.Owner);

    // Documents of the animals the user owns, in any state. Tracked, for the upload operation only.
    private IQueryable<Document> ForUser(string userId)
    {
        var memberships = OwnerMemberships(userId);
        return db.Documents.Where(d => memberships.Any(m => m.AnimalId == d.AnimalId));
    }

    private IQueryable<Document> StoredForUser(string userId) =>
        ForUser(userId).AsNoTracking().Where(d => d.StorageState == DocumentStorageState.Stored);
}
