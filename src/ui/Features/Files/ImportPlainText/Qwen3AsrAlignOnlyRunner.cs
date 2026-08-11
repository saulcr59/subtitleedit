using Nikse.SubtitleEdit.Logic.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Files.ImportPlainText;

/// <summary>
/// Drives <c>qwen3-asr-cli --align</c> with a wav2vec2 CTC model: audio plus the text
/// spoken in it, in and time codes out, with no transcription step in between.
/// <para>
/// Unlike crispasr, which can emit one cue per input line directly, this CLI reports
/// per-token times, so the tokens are regrouped here. That is safe because forced
/// alignment cannot invent or drop symbols - the tokens are the input text, tokenised -
/// and it keeps the CLI free of a subtitle-granularity concept it has no other use for.
/// </para>
/// </summary>
public sealed class Qwen3AsrAlignOnlyRunner : ForcedAligner.IRunner
{
    private readonly string _executable;
    private readonly string _alignerModel;
    private readonly Action<string>? _log;

    public Qwen3AsrAlignOnlyRunner(string executable, string alignerModel, Action<string>? log = null)
    {
        _executable = executable;
        _alignerModel = alignerModel;
        _log = log;
    }

    public async Task<string> AlignAsync(string audioFileName, string textFileName, CancellationToken cancellationToken)
    {
        var outputFileName = Path.ChangeExtension(audioFileName, ".aligned.json");

        var arguments =
            $"--align --ctc-align-model \"{_alignerModel}\" -f \"{audioFileName}\" " +
            $"--text-file \"{textFileName}\" -o \"{outputFileName}\"";

        Se.WriteToolsLog($"{_executable} {arguments}");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(_executable, arguments)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(_executable),
            },
        };

        var stdErr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            lock (stdErr)
            {
                stdErr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0 || !File.Exists(outputFileName))
        {
            var detail = stdErr.ToString().Trim();
            _log?.Invoke(detail);

            // The encoder allocates its activations up front, so a window too long for
            // the available memory fails outright rather than degrading.
            if (detail.Contains("failed to allocate", StringComparison.OrdinalIgnoreCase))
            {
                throw new ForcedAlignerException(
                    "The forced aligner ran out of memory on this window. Try a shorter window length.", detail);
            }

            throw new ForcedAlignerException($"The forced aligner failed (exit code {process.ExitCode}).", detail);
        }

        try
        {
            var json = await File.ReadAllTextAsync(outputFileName, cancellationToken).ConfigureAwait(false);
            var fedLines = await File.ReadAllLinesAsync(textFileName, cancellationToken).ConfigureAwait(false);
            return BuildSrt(fedLines, ParseWords(json));
        }
        finally
        {
            TryDelete(outputFileName);
        }
    }

    private sealed class AlignedWord
    {
        [JsonPropertyName("word")] public string Word { get; set; } = string.Empty;
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("end")] public double End { get; set; }
    }

    private sealed class AlignedOutput
    {
        [JsonPropertyName("words")] public List<AlignedWord>? Words { get; set; }
    }

    internal static IReadOnlyList<(string Text, double Start, double End)> ParseWords(string json)
    {
        var parsed = JsonSerializer.Deserialize<AlignedOutput>(json);
        var result = new List<(string, double, double)>();
        if (parsed?.Words == null)
        {
            return result;
        }

        foreach (var w in parsed.Words)
        {
            result.Add((w.Word, w.Start, w.End));
        }

        return result;
    }

    /// <summary>
    /// Regroups per-token times into one cue per fed line, which is the mapping
    /// <see cref="ForcedAligner.ParseCues"/> relies on.
    /// <para>
    /// Tokens are consumed in order and counted by their non-whitespace length: a line
    /// owns as many tokens as it takes to account for its own characters. Whitespace is
    /// ignored on both sides because the tokeniser drops it for CJK and turns it into
    /// word boundaries elsewhere, so it is not represented in the token stream.
    /// </para>
    /// </summary>
    internal static string BuildSrt(IReadOnlyList<string> fedLines, IReadOnlyList<(string Text, double Start, double End)> words)
    {
        var sb = new StringBuilder();
        var wordIndex = 0;
        var number = 1;
        var lastEnd = 0.0;

        foreach (var line in fedLines)
        {
            var needed = CountVisible(line);
            double? start = null;
            var end = lastEnd;
            var got = 0;

            while (wordIndex < words.Count && got < needed)
            {
                var w = words[wordIndex];
                start ??= w.Start;
                end = w.End;
                got += CountVisible(w.Text);
                wordIndex++;
            }

            // A line with no tokens left to claim still needs a cue: ParseCues maps
            // cues onto lines by position, so skipping one would shift every line
            // after it. Give it a zero-length cue at the last known time instead.
            var startSeconds = start ?? lastEnd;
            lastEnd = end;

            sb.Append(number.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(ToSrtTime(startSeconds)).Append(" --> ").Append(ToSrtTime(end)).Append('\n');
            sb.Append(line).Append('\n').Append('\n');
            number++;
        }

        return sb.ToString();
    }

    private static int CountVisible(string s)
    {
        var n = 0;
        foreach (var c in s)
        {
            if (!char.IsWhiteSpace(c))
            {
                n++;
            }
        }

        return n;
    }

    private static string ToSrtTime(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds))
        {
            seconds = 0;
        }

        var ts = TimeSpan.FromSeconds(seconds);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:00}:{1:00}:{2:00},{3:000}",
            (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // Killing a process that just exited on its own is not an error.
        }
    }

    private static void TryDelete(string fileName)
    {
        try
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }
        catch
        {
            // Temp cleanup is best effort.
        }
    }
}
