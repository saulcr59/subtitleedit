using Nikse.SubtitleEdit.Features.Files.ImportPlainText;

namespace UITests.Features.Files.ImportPlainText;

/// <summary>
/// AcceptChunk decides how much of a window's answer to believe. Rejecting too eagerly is
/// not a correctness failure - the lines are simply realigned on a fresh window - so it
/// shows up only as a run that takes one window per line.
/// </summary>
public class AcceptChunkSlackTests
{
    private static ForcedAlignPlanner.Cue Cue(double start, double end) => new(start, end);

    [Fact]
    public void AShortLineIsNotMistakenForARunawayCue()
    {
        // Verbatim from Japanese speech at the default 15 chars/second display setting:
        // seven characters read in 0.47 s take 2.80 s to say.
        var cues = new[] { Cue(0.0, 1.0), Cue(1.0, 3.8) };
        var reading = new[] { 0.60, 0.47 };

        Assert.Equal(1, ForcedAlignPlanner.AcceptChunk(cues, reading));
        Assert.Equal(2, ForcedAlignPlanner.AcceptChunk(cues, reading, maxDurationSlackSeconds: 2.0));
    }

    [Fact]
    public void ARunawayCueIsStillRejectedWithSlack()
    {
        // The failure the check exists for: the aligner runs out of script and smears the
        // remaining cues down the window. Slack must not blind it to that.
        var cues = new[] { Cue(0.0, 1.0), Cue(1.0, 55.0) };
        var reading = new[] { 0.60, 0.93 };

        Assert.Equal(1, ForcedAlignPlanner.AcceptChunk(cues, reading, maxDurationSlackSeconds: 2.0));
    }

    [Fact]
    public void ALongSilenceBetweenCuesStillEndsTheChunk()
    {
        // Consecutive script lines are consecutive speech, so a big gap means the aligner
        // has stopped tracking - slack is about duration and must not affect this.
        var cues = new[] { Cue(0.0, 1.0), Cue(30.0, 31.0) };
        var reading = new[] { 0.60, 0.60 };

        Assert.Equal(1, ForcedAlignPlanner.AcceptChunk(cues, reading, maxDurationSlackSeconds: 2.0));
    }

    [Fact]
    public void SlackDefaultsToZeroSoExistingCallersAreUnchanged()
    {
        var cues = new[] { Cue(0.0, 1.0), Cue(1.0, 3.8) };
        var reading = new[] { 0.60, 0.47 };

        Assert.Equal(
            ForcedAlignPlanner.AcceptChunk(cues, reading),
            ForcedAlignPlanner.AcceptChunk(cues, reading, maxDurationSlackSeconds: 0.0));
        Assert.Equal(0.0, new ForcedAlignPlanner.Options().MaxDurationSlackSeconds);
    }

    [Fact]
    public void TheMeasuredRatiosFromRealSpeechAreAllAccepted()
    {
        // Every line from the 60 s sample that drove this change, as (duration, reading).
        var measured = new[]
        {
            (1.10, 0.60), (3.56, 0.93), (2.56, 1.33), (2.72, 0.93),
            (2.88, 1.33), (1.20, 0.93), (2.82, 0.73), (2.80, 0.47),
        };

        var cues = new List<ForcedAlignPlanner.Cue>();
        var reading = new List<double>();
        var t = 0.0;
        foreach (var (duration, read) in measured)
        {
            cues.Add(Cue(t, t + duration));
            reading.Add(read);
            t += duration;
        }

        // Acceptance stops at the first violation rather than skipping it, so the second
        // line - 3.56 s against 0.93 s of reading time - ended the chunk on its own and
        // exactly one cue survived per window, however many lines were fed in.
        Assert.Equal(1, ForcedAlignPlanner.AcceptChunk(cues, reading));
        Assert.Equal(measured.Length, ForcedAlignPlanner.AcceptChunk(cues, reading, maxDurationSlackSeconds: 2.0));
    }
}
