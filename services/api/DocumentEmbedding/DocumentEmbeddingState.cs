namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Where embedding a read document's text is. Separate from the reading state: the original and its text stay
// retrievable whatever this says.
public enum DocumentEmbeddingState
{
    // Waiting for a worker; due at DueAt.
    Queued,

    // Claimed by a worker that holds an unexpired lease. An expired lease can be reclaimed.
    Embedding,

    // The document's chunks and vectors are stored for EmbeddedRunGeneration and EmbeddedAnimalName.
    Embedded,

    // Embedding stopped with a sanitized FailureCode. Only a new enqueue (a re-read or a rename) queues it again.
    Failed,
}
