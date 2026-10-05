namespace ogarniamy_zwierzaki_api.DocumentReading;

// Optical character recognition by the provider, split into resumable steps so the worker can save the operation ID
// before waiting: submit once (never replayed), poll the saved ID (also after a restart), then delete the result.
// Every failure is a DocumentReadFailure with a sanitized code.
public interface IDocumentTextReader
{
    // Sends the original's bytes once and returns the provider's analysis result ID as soon as it is known. pages
    // selects original page numbers of a PDF in the provider's syntax ("2,4-5"); null reads the whole file. A failure
    // whose outcome is unknown (timeout, lost response) is transient and is never replayed by this call.
    Task<string> SubmitAsync(Stream content, string contentType, string? pages, CancellationToken cancellationToken);

    // The analysis of a saved result ID: running, or complete with the text of each analysed page.
    Task<OcrAnalysis> GetAnalysisAsync(string operationId, CancellationToken cancellationToken);

    // Requests deletion of the result. Completes when the provider acknowledged it (204) or reports it missing (404);
    // an acknowledgement is not proof of physical erasure. Throws a DocumentReadFailure otherwise.
    Task DeleteResultAsync(string operationId, CancellationToken cancellationToken);
}
