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
            var aligner = new ForcedAligner(runner, audio);

            var result = await aligner.AlignAsync(lines, null, TestContext.Current.CancellationToken);

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
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* best effort */ }
        }
    }
}
