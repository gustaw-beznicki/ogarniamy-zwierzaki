namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// How a chunk becomes provider input, and the version string that records every choice that shapes a stored vector.
public static class EmbeddingInput
{
    // Bumped when the input scheme below changes.
    public const int SchemeVersion = 1;

    // The animal's current name, a blank line, then the chunk text, so a query that names the animal matches its
    // documents.
    public static string For(string animalName, string chunkText) => $"{animalName}\n\n{chunkText}";

    // Part of every stored chunk: a later chunker, model, dimension or input-scheme change makes old chunks recognisable.
    public static string Version(string deployment, int dimensions) =>
        $"chunker-v{PageChunker.Version}/{deployment}/{dimensions}d/input-v{SchemeVersion}";
}
