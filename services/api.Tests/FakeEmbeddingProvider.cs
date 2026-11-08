using System.Text;
using ogarniamy_zwierzaki_api.DocumentEmbedding;

namespace ogarniamy_zwierzaki_api.Tests;

// A deterministic IEmbeddingClient for worker and search tests: a hashed bag-of-words vector, so texts that share words
// are similar and unrelated texts are not. Every call is recorded (inputs, in call order); OnEmbed runs before the
// answer and may throw an EmbeddingFailure or change the database to simulate a concurrent edit. Nothing leaves the
// process.
public sealed class FakeEmbeddingProvider : IEmbeddingClient
{
    private readonly object _gate = new();
    private readonly List<IReadOnlyList<string>> _calls = [];

    // The inputs of every EmbedAsync call so far.
    public IReadOnlyList<IReadOnlyList<string>> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    // Called with the inputs and the call's 1-based index before any vector is produced.
    public Func<IReadOnlyList<string>, int, Task>? OnEmbed { get; set; }

    public async Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        int index;
        lock (_gate)
        {
            _calls.Add(inputs);
            index = _calls.Count;
        }

        if (OnEmbed is { } hook)
        {
            await hook(inputs, index);
        }

        return inputs.Select(Embed).ToList();
    }

    // Each lowercase word (letters and digits) adds one to a stable hash bucket; the vector is L2-normalised, so the dot
    // product of two vectors is their cosine similarity. A text without words is the first unit vector.
    public static float[] Embed(string text)
    {
        var vector = new float[EmbeddingOptions.StoredDimensions];
        var word = new StringBuilder();
        foreach (var character in text.ToLowerInvariant().Append(' '))
        {
            if (char.IsLetterOrDigit(character))
            {
                word.Append(character);
            }
            else if (word.Length > 0)
            {
                vector[(int)(Fnv1a(word.ToString()) % (uint)vector.Length)] += 1;
                word.Clear();
            }
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm == 0)
        {
            vector[0] = 1;
            return vector;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }

        return vector;
    }

    public static float Cosine(float[] left, float[] right) => left.Zip(right, (a, b) => a * b).Sum();

    private static uint Fnv1a(string value)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * 16777619u;
        }

        return hash;
    }
}
