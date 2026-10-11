namespace ogarniamy_zwierzaki_api.DocumentReading;

// How far reading one original of one run got. A run file is resumable while the job keeps its run generation.
public enum DocumentReadingRunFileStage
{
    // Started; nothing has been submitted to the provider yet.
    Pending,

    // Submitted to the provider; ProviderOperationId is known and can be polled after a restart.
    Submitted,

    // The text is checkpointed in FetchedText.
    Retrieved,

    // Abandoned: the attempt failed for good or its lease was lost. Never resumed; any provider result is only cleaned up.
    Failed,
}
