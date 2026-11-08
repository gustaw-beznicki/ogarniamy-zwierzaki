namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// The outcome of DocumentEmbeddingJobs.PublishAsync.
public enum DocumentEmbeddingPublication
{
    // The document's chunks were replaced and the job is Embedded.
    Embedded,

    // The reading revision or the animal name changed while embedding: the result was discarded and the job re-queued.
    Stale,

    // The lease expired or was taken over (or a new enqueue reset the job): nothing was written.
    LeaseLost,
}
