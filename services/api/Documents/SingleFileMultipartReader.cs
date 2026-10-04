using System.Buffers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace ogarniamy_zwierzaki_api.Documents;

// Reads a multipart/form-data body holding exactly one file in the "file" field into memory, at most
// DocumentFile.MaxByteLength bytes. The bytes are buffered (one file per request) because they are verified against
// the manifest before anything is written to storage, and the storage adapter needs a seekable stream.
// The body is read directly instead of through IFormFile binding, so an oversized file is answered with a clean
// 413 file_too_large and nothing beyond the limit is buffered.
public static class SingleFileMultipartReader
{
    public const string FileFieldName = "file";

    // The largest accepted request: one maximum-size file plus room for the multipart framing.
    public const long MaxRequestBytes = 11 * 1024 * 1024;

    private const int CopyBufferSize = 81_920;

    // The longest boundary RFC 2046 allows.
    private const int MaxBoundaryLength = 70;

    public static async Task<CaptureResult<MemoryStream>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxRequestBytes)
        {
            return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.FileTooLarge);
        }

        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var mediaType)
            || !mediaType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.UnsupportedMediaType);
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > MaxBoundaryLength)
        {
            return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.InvalidUploadBody);
        }

        var reader = new MultipartReader(boundary, request.Body);
        try
        {
            var section = await reader.ReadNextSectionAsync(cancellationToken);
            if (section is null
                || !ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.IsFileDisposition()
                || HeaderUtilities.RemoveQuotes(disposition.Name).Value != FileFieldName)
            {
                return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.InvalidUploadBody);
            }

            var content = new MemoryStream(
                (int)Math.Clamp(request.ContentLength ?? 0, 0, DocumentFile.MaxByteLength));
            if (!await CopyWithinLimitAsync(section.Body, content, cancellationToken))
            {
                await content.DisposeAsync();
                return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.FileTooLarge);
            }

            if (await reader.ReadNextSectionAsync(cancellationToken) is not null)
            {
                await content.DisposeAsync();
                return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.InvalidUploadBody);
            }

            content.Position = 0;
            return CaptureResult<MemoryStream>.Ok(content);
        }
        catch (InvalidDataException)
        {
            // Malformed multipart framing.
            return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.InvalidUploadBody);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // The server's request size limit for this route (MaxRequestBytes) was reached.
            return CaptureResult<MemoryStream>.Fail(DocumentCaptureFailure.FileTooLarge);
        }
    }

    // Copies the file; false as soon as it exceeds DocumentFile.MaxByteLength.
    private static async Task<bool> CopyWithinLimitAsync(Stream source, MemoryStream destination, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
            {
                if (destination.Length + read > DocumentFile.MaxByteLength)
                {
                    return false;
                }

                destination.Write(buffer, 0, read);
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
