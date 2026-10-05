using System.Diagnostics;
using System.Text.Json;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// Reads a PDF's own text layer locally, in a child process (PdfTextReaderProcess) with a parse timeout and a cap on
// its output, so a hostile or pathological PDF cannot take the API down. The PDF is streamed to the child's standard
// input: nothing is written to disk and there is no source cache. Never logs text.
public sealed class PdfTextReader
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);

    // Room for the 10,000,000-character limit in UTF-8 plus JSON overhead; more is treated as a parser failure.
    public long MaxOutputBytes { get; init; } = 48L * 1024 * 1024;

    // A page's text layer can be used as is when it has a letter or digit, no replacement, null or control characters
    // (other than whitespace) that betray a broken font mapping, and no raster image that may hold scanned text.
    public static bool IsUsable(PdfLocalPage page)
    {
        if (page.HasRasterImage)
        {
            return false;
        }

        var hasLetterOrDigit = false;
        foreach (var character in page.Text)
        {
            if (character is '�' or '\0' || (char.IsControl(character) && !char.IsWhiteSpace(character)))
            {
                return false;
            }

            hasLetterOrDigit |= char.IsLetterOrDigit(character);
        }

        return hasLetterOrDigit;
    }

    // The pages of the PDF, 1..n. Throws a DocumentReadFailure: encrypted_pdf and page_limit_exceeded are terminal;
    // pdf_unreadable (malformed input, timeout, oversized output, a crashed child) allows the provider to try instead.
    public async Task<IReadOnlyList<PdfLocalPage>> ReadAsync(Stream pdf, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = HelperStartInfo() };
        if (!process.Start())
        {
            throw DocumentReadFailure.LocalParserFailed();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            var stdout = ReadCappedAsync(process.StandardOutput.BaseStream, timeout);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await WriteInputAsync(process, pdf, timeout.Token);
            var output = await stdout;
            await stderr;
            await process.WaitForExitAsync(timeout.Token);
            if (output is null || process.ExitCode != 0)
            {
                throw DocumentReadFailure.LocalParserFailed();
            }

            return Interpret(output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The parse timeout.
            throw DocumentReadFailure.LocalParserFailed();
        }
        catch (IOException)
        {
            // The child closed its pipes early (crashed or was killed).
            throw DocumentReadFailure.LocalParserFailed();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private static IReadOnlyList<PdfLocalPage> Interpret(byte[] output)
    {
        PdfTextHelperOutput? result;
        try
        {
            result = JsonSerializer.Deserialize<PdfTextHelperOutput>(output, PdfTextReaderProcess.JsonOptions);
        }
        catch (JsonException)
        {
            throw DocumentReadFailure.LocalParserFailed();
        }

        return result?.Status switch
        {
            PdfTextHelperOutput.Ok when result.Pages is { Count: > 0 } pages
                && pages.Select(p => p.PageNumber).SequenceEqual(Enumerable.Range(1, pages.Count))
                && pages.All(p => p.Text is not null) => pages,
            PdfTextHelperOutput.Encrypted => throw DocumentReadFailure.Terminal(DocumentReadFailure.EncryptedPdf),
            PdfTextHelperOutput.PageLimit => throw DocumentReadFailure.Terminal(DocumentReadFailure.PageLimitExceeded),
            _ => throw DocumentReadFailure.LocalParserFailed(),
        };
    }

    private static async Task WriteInputAsync(Process process, Stream pdf, CancellationToken cancellationToken)
    {
        try
        {
            await pdf.CopyToAsync(process.StandardInput.BaseStream, cancellationToken);
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (IOException)
            {
                // The child already exited; its output tells what happened.
            }
        }
    }

    // Null when the output exceeds the cap; the child is then killed.
    private async Task<byte[]?> ReadCappedAsync(Stream stdout, CancellationTokenSource timeout)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await stdout.ReadAsync(chunk, timeout.Token)) > 0)
        {
            if (buffer.Length + read > MaxOutputBytes)
            {
                await timeout.CancelAsync();
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    // The API's own assembly in its hidden helper mode, run by the same .NET host as the API.
    private static ProcessStartInfo HelperStartInfo()
    {
        var assemblyPath = typeof(PdfTextReaderProcess).Assembly.Location;
        var processPath = Environment.ProcessPath;
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (processPath is not null
            && Path.GetFileNameWithoutExtension(processPath) == Path.GetFileNameWithoutExtension(assemblyPath))
        {
            // A self-contained or apphost launch: the executable is the API itself.
            startInfo.FileName = processPath;
        }
        else
        {
            startInfo.FileName = DotnetHostPath(processPath);
            startInfo.ArgumentList.Add(assemblyPath);
        }

        startInfo.ArgumentList.Add(PdfTextReaderProcess.Command);
        // Bounds the child's managed heap (256 MiB, within the F1 plan's memory), so a pathological PDF fails the parse
        // instead of the app.
        startInfo.Environment["DOTNET_GCHeapHardLimit"] = "0x10000000";
        // One core and no background GC thread: CPU time stays close to the wall-clock timeout, so one parse can use at
        // most about a minute of the F1 daily CPU quota (exhausting it stops the app and its originals).
        startInfo.Environment["DOTNET_PROCESSOR_COUNT"] = "1";
        startInfo.Environment["DOTNET_gcConcurrent"] = "0";
        // The parser reads no configuration, so it gets none of the API's connection strings or app settings.
        foreach (var name in startInfo.Environment.Keys
            .Where(key => key.StartsWith("ConnectionStrings", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("CUSTOMCONNSTR_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("POSTGRESQLCONNSTR_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("APPSETTING_", StringComparison.OrdinalIgnoreCase))
            .ToList())
        {
            startInfo.Environment.Remove(name);
        }

        return startInfo;
    }

    private static string DotnetHostPath(string? processPath)
    {
        if (processPath is not null && Path.GetFileNameWithoutExtension(processPath) == "dotnet")
        {
            return processPath;
        }

        // Set by the .NET SDK for processes it starts (for example the test host).
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath && File.Exists(hostPath))
        {
            return hostPath;
        }

        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root)
        {
            var candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return "dotnet";
    }
}
