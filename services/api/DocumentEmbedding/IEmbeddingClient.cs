namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Turns texts into embedding vectors. Implementations throw EmbeddingFailure for every provider problem, with a
// sanitized code and a transient/terminal classification, and never put the inputs into a message or a log.
public interface IEmbeddingClient
{
    // One vector per input, in input order, each EmbeddingOptions.Dimensions long. An empty input list gives an empty
    // result without calling the provider.
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken);
}
