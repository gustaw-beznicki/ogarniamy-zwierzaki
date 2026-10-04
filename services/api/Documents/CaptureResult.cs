namespace ogarniamy_zwierzaki_api.Documents;

// The outcome of a DocumentCaptureService call: a value, or the reason it failed. Created tells a first creation
// apart from an identical retry.
public sealed record CaptureResult<T>(T? Value, DocumentCaptureFailure? Failure, bool Created = false)
    where T : class
{
    public static CaptureResult<T> Ok(T value, bool created = false) => new(value, null, created);

    public static CaptureResult<T> Fail(DocumentCaptureFailure failure) => new(null, failure);
}
