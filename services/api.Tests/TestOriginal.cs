using System.Security.Cryptography;

namespace ogarniamy_zwierzaki_api.Tests;

// A generated, non-sensitive original: a format signature followed by random bytes, of an exact length.
public sealed record TestOriginal(string Name, string ContentType, byte[] Bytes)
{
    public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Bytes));

    public static TestOriginal Pdf(int length = 2_000, string name = "scan.pdf") =>
        new(name, "application/pdf", Generate("%PDF-1.7\n"u8, length));

    public static TestOriginal Jpeg(int length = 1_500, string name = "photo.jpg") =>
        new(name, "image/jpeg", Generate([0xFF, 0xD8, 0xFF, 0xE0], length));

    public static TestOriginal Png(int length = 1_200, string name = "photo.png") =>
        new(name, "image/png", Generate([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], length));

    // The manifest entry the browser sends for this original.
    public object ToManifestFile() => new { name = Name, contentType = ContentType, byteLength = Bytes.LongLength, sha256 = Sha256 };

    private static byte[] Generate(ReadOnlySpan<byte> signature, int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        signature[..Math.Min(signature.Length, length)].CopyTo(bytes);
        return bytes;
    }
}
