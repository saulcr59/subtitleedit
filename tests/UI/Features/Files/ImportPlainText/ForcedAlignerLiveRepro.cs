using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Features.Files.ImportPlainText;
using Nikse.SubtitleEdit.Features.Main;
using System.Diagnostics;

namespace UITests.Features.Files.ImportPlainText;

/// <summary>
/// Runs the real forced-aligner pipeline - runner, window source, planner - against a
/// local video. Skipped unless the fixture files happen to be present, so it is inert on
/// CI and on anyone else's machine; it exists to reproduce integration failures that unit
/// tests cannot see, where every part works alone and the assembly of them does not.
/// </summary>
public class ForcedAlignerLiveRepro
{
    private const string Video = @"C:\Users\Saul\Documents\Youtube Videos\【 謎 】ディスプレイ付きの電子ガジェット.mkv";
    private const string Srt = @"C:\Users\Saul\Documents\Youtube Videos\【 謎 】ディスプレイ付きの電子ガジェット.CTC-CPP.srt";
    // A build run from anywhere outside Program Files is portable, so its data folder is
    // the executable's own folder rather than %AppData%. Resolve the engine the same way
    // the app does, or the test checks a different install than the one being changed.
    private static readonly string DataFolder = AppContext.BaseDirectory;
    private static readonly string Engine = Path.Combine(DataFolder, "Qwen3ASR", "qwen3-asr-cli.exe");
    private static readonly string Model =
        Path.Combine(DataFolder, "Qwen3ASR", "models", "wav2vec2-ctc-ja-ivydata-f16.gguf");

    private static string FfmpegPath()
    {
        var bundled = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Subtitle Edit", "ffmpeg", "ffmpeg.exe");
        return File.Exists(bundled) ? bundled : "ffmpeg";
    }

    [AvaloniaFact]
    public async Task AlignsTheFirstLinesOntoRealSpeech()
    {
        if (!File.Exists(Video) || !File.Exists(Srt) || !File.Exists(Engine) || !File.Exists(Model))
        {
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "se-repro-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var wav = Path.Combine(work, "audio.wav");

        try
        {
            // Deliberately MP3, which is what the online engines upload and therefore what
            // the alignment step is handed. Windows are cut with `-c copy`, so an MP3
            // source yields MP3 windows the aligner cannot read - the failure this
            // reproduces. The pipeline has to transcode before windowing.
            var mp3 = Path.Combine(work, "uploaded.mp3");
            var psi = new ProcessStartInfo(FfmpegPath(),
                $"-y -v error -i \"{Video}\" -t 60 -vn \"{mp3}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            using (var ff = Process.Start(psi)!)
            {
                await ff.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(mp3), "ffmpeg produced no audio");

            var psi2 = new ProcessStartInfo(FfmpegPath(),
                $"-y -v error -i \"{mp3}\" -vn -ar 16000 -ac 1 -acodec pcm_s16le \"{wav}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            using (var ff = Process.Start(psi2)!)
            {
                await ff.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            Assert.True(File.Exists(wav), "transcode to 16 kHz PCM produced no audio");

            var source = new Subtitle();
            new SubRip().LoadSubtitle(source, File.ReadAllLines(Srt).ToList(), Srt);
            var lines = source.Paragraphs.Take(8)
                .Select(p => new SubtitleLineViewModel(new Paragraph(p.Text, 0, 0), new SubRip()))
                .ToList();

            using var audio = new FfmpegWindowAudioSource(FfmpegPath(), wav, 60.0, work);
            var runner = new Qwen3AsrAlignOnlyRunner(Engine, Model, m => Console.WriteLine("[cli] " + m));

            // Same settings the speech-to-text path uses, so this exercises what ships
            // rather than the library defaults.
            var aligner = new ForcedAligner(runner, audio, new ForcedAlignPlanner.Options
            {
                WindowSeconds = 45,
                MaxDurationSlackSeconds = 2.0,
                TrustMeasuredDurations = true,
            });

            var windowsUsed = 0;
            var progress = new Progress<ForcedAligner.Progress>(p => windowsUsed = Math.Max(windowsUsed, p.WindowIndex));

            var result = await aligner.AlignAsync(lines, progress, TestContext.Current.CancellationToken);

            Console.WriteLine($"aligned {result.AlignedLines}/{result.TotalLines}");
            foreach (var line in lines.Take(4))
            {
                Console.WriteLine($"  {line.StartTime.TotalSeconds,7:F3} -> {line.EndTime.TotalSeconds,7:F3}  {line.Text}");
            }

            // Silero puts the first speech in this file at 7.810 s. Interpolated time codes
            // start the first line near zero; before the swallowed-silence correction this
            // landed at 8.643 s, past the end of the phrase. Both failures are outside this
            // range, so it distinguishes a correct alignment from either.
            var first = lines[0].StartTime.TotalSeconds;
            Assert.True(first is > 6.5 and < 8.3,
                $"first line starts at {first:F3}s; speech runs 7.810-8.670s");

            Assert.True(result.AlignedLines >= lines.Count - 1,
                $"only {result.AlignedLines}/{result.TotalLines} lines were accepted");

            // The real symptom of untrimmed cue ends: a cue reaching out into the silence
            // after it looks far longer than its text takes to read, which AcceptChunk
            // treats as the aligner having lost track, so it believed exactly one cue per
            // window. Lines accepted cannot show that - they were all accepted either way,
            // one window at a time - so assert on the windows it took instead.
            Assert.True(windowsUsed <= 2,
                $"took {windowsUsed} windows for {lines.Count} lines; cues are being rejected one per window");

            // Silero puts the second line's speech at 9.218-11.710 s. Aligning one line per
            // window made each line restart from wherever the last one ended, and the error
            // compounded: this line came out at 11.783 s, after the phrase had finished.
            var second = lines[1].StartTime.TotalSeconds;
            Assert.True(second is > 8.5 and < 10.0,
                $"second line starts at {second:F3}s; its speech runs 9.218-11.710s");

            // Durations are recomputed from reading time but then clipped so a line cannot
            // outlast the next line's start. Lines crowded together therefore come out
            // flashing past - the run that misplaced its cues produced several under 0.5 s.
            for (var i = 0; i < lines.Count; i++)
            {
                var seconds = lines[i].EndTime.TotalSeconds - lines[i].StartTime.TotalSeconds;
                Assert.True(seconds >= 0.5,
                    $"line {i + 1} is on screen for {seconds:F3}s: \"{lines[i].Text}\"");
            }

            // Speech is slower than reading, so capping duration at reading time pinned
            // almost every cue to the one-second minimum and made it vanish mid-sentence.
            // These lines take 1.1-3.6 s to say, so several must outlast that minimum.
            var overMinimum = lines.Count(l => (l.EndTime - l.StartTime).TotalSeconds > 1.5);
            Assert.True(overMinimum >= 3,
                $"only {overMinimum} of {lines.Count} cues last longer than 1.5s; durations are still capped at reading time");

            for (var i = 1; i < lines.Count; i++)
            {
                Assert.True(lines[i].StartTime >= lines[i - 1].StartTime,
                    $"line {i + 1} starts before line {i}");
            }
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }
}
