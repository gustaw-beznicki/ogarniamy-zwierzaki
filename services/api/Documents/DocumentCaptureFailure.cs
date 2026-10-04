namespace ogarniamy_zwierzaki_api.Documents;

// Why a capture or original request failed. DocumentEndpoints maps each value to an HTTP status and a ProblemDetails
// `code`; the comment names that code.
public enum DocumentCaptureFailure
{
    // 404 without a body: the operation, document, file slot or animal is missing or belongs to another account.
    NotFound,

    // 400 animal_required
    AnimalRequired,

    // 400 invalid_event_date: missing or not an ISO yyyy-MM-dd calendar date.
    InvalidEventDate,

    // 400 future_event_date: later than today in the supplied time zone.
    FutureEventDate,

    // 400 invalid_time_zone: missing, unknown or not an IANA time zone ID.
    InvalidTimeZone,

    // 400 invalid_file_set: no files, more than ten images, or a PDF together with other files.
    InvalidFileSet,

    // 400 invalid_file_name: empty after sanitizing, or longer than 255 characters.
    InvalidFileName,

    // 400 invalid_file_hash: not a 64-character hex SHA-256.
    InvalidFileHash,

    // 400 empty_file
    EmptyFile,

    // 413 file_too_large: more than 10,485,760 bytes.
    FileTooLarge,

    // 415 unsupported_file_type: not a PDF, JPEG or PNG, by declared type or by content signature.
    UnsupportedFileType,

    // 415 unsupported_media_type: a file upload that is not multipart/form-data.
    UnsupportedMediaType,

    // 400 invalid_upload_body: the multipart body does not hold exactly one file in the "file" field.
    InvalidUploadBody,

    // 400 file_mismatch: the uploaded bytes differ from the manifest in length, SHA-256 or format.
    FileMismatch,

    // 409 upload_conflict: the manifest differs from the operation's frozen one, or a different original is stored.
    UploadConflict,

    // 409 upload_incomplete: completion was requested before every original was durably stored.
    UploadIncomplete,

    // 400 invalid_pagination
    InvalidPagination,

    // 503 storage_unavailable: a transient Blob Storage or database failure; the request can be retried.
    StorageUnavailable,

    // 503 original_unavailable: a Stored document's original cannot be found in Blob Storage.
    OriginalUnavailable,
}
