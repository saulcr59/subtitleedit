using Nikse.SubtitleEdit.Features.Files.ImportPlainText;

namespace UITests.Features.Files.ImportPlainText;

/// <summary>
/// The regrouping from per-token times to one-cue-per-line is what
/// <see cref="ForcedAligner.ParseCues"/> maps back onto script lines by position, so a
/// mistake here does not fail loudly - it silently shifts every following line's timing.
/// </summary>
public class Qwen3AsrAlignOnlyRunnerTests
{
    private static (string, double, double) W(string text, double start, double end) => (text, start, end);

    [Fact]
    public void GroupsCjkCharactersOntoTheirOwnLines()
    {
        var lines = new[] { "実は、", "岬漁港" };
        var words = new[]
        {
            W("実", 1.0, 1.2), W("は", 1.2, 1.4), W("、", 1.4, 1.5),
            W("岬", 2.0, 2.3), W("漁", 2.3, 2.5), W("港", 2.5, 2.9),
        };

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(lines, words));

        Assert.Equal(2, cues.Count);
        Assert.Equal(1.0, cues[0].StartSeconds, 3);
        Assert.Equal(1.5, cues[0].EndSeconds, 3);
        Assert.Equal(2.0, cues[1].StartSeconds, 3);
        Assert.Equal(2.9, cues[1].EndSeconds, 3);
    }

    [Fact]
    public void GroupsWholeWordsForSpaceSeparatedText()
    {
        // Latin text tokenises to words, not characters, and the fed lines contain
        // spaces the token stream does not - both sides must ignore whitespace.
        var lines = new[] { "hello there", "general kenobi" };
        var words = new[]
        {
            W("hello", 0.5, 0.9), W("there", 0.9, 1.4),
            W("general", 2.0, 2.6), W("kenobi", 2.6, 3.1),
        };

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(lines, words));

        Assert.Equal(2, cues.Count);
        Assert.Equal(0.5, cues[0].StartSeconds, 3);
        Assert.Equal(1.4, cues[0].EndSeconds, 3);
        Assert.Equal(2.0, cues[1].StartSeconds, 3);
        Assert.Equal(3.1, cues[1].EndSeconds, 3);
    }

    [Fact]
    public void EmitsOneCuePerLineEvenWhenTokensRunOut()
    {
        // A short token stream must not drop trailing lines: ParseCues maps by
        // position, so a missing cue would shift every later line's timing.
        var lines = new[] { "実は", "岬漁港", "城島" };
        var words = new[] { W("実", 1.0, 1.2), W("は", 1.2, 1.4) };

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(lines, words));

        Assert.Equal(3, cues.Count);
        Assert.Equal(1.0, cues[0].StartSeconds, 3);
    }

    [Fact]
    public void CuesNeverGoBackwards()
    {
        var lines = new[] { "あい", "うえ", "お" };
        var words = new[]
        {
            W("あ", 0.1, 0.4), W("い", 0.4, 0.8),
            W("う", 1.0, 1.3), W("え", 1.3, 1.6),
            W("お", 2.0, 2.4),
        };

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(lines, words));

        for (var i = 1; i < cues.Count; i++)
        {
            Assert.True(cues[i].StartSeconds >= cues[i - 1].StartSeconds,
                $"cue {i} starts before cue {i - 1}");
        }
    }

    [Fact]
    public void ALineWhoseFirstCharacterSwallowedSilenceStartsAtTheSound()
    {
        // Verbatim from the aligner on real audio: speech starts at 7.81 s, but the first
        // character is reported as spanning 0.160-7.688 s because CTC assigns the silent
        // frames to it. Taking that start verbatim put the cue 7 s early.
        var lines = new[] { "どうも、" };
        var words = new[]
        {
            W("ど", 0.160, 7.688), W("う", 7.688, 7.788),
            W("も", 7.788, 7.888), W("、", 7.888, 7.928),
        };

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(lines, words));

        Assert.Single(cues);
        Assert.True(cues[0].StartSeconds > 7.0,
            $"cue starts at {cues[0].StartSeconds:F3}s, in the silence before the speech");
        Assert.True(cues[0].StartSeconds <= 7.688, "cue must not start after the sound does");
    }

    [Fact]
    public void AnOrdinaryLineKeepsItsReportedStart()
    {
        var claimed = new[] { (1.00, 1.14), (1.14, 1.26), (1.26, 1.40) };

        Assert.Equal(1.00, Qwen3AsrAlignOnlyRunner.OnsetOf(claimed), 3);
    }

    [Fact]
    public void ASingleCharacterLineHasNothingToCompareAgainst()
    {
        var claimed = new[] { (0.16, 7.69) };

        Assert.Equal(0.16, Qwen3AsrAlignOnlyRunner.OnsetOf(claimed), 3);
    }

    [Fact]
    public void ASlightlyLongFirstCharacterIsNotTrimmed()
    {
        // Twice the typical length is ordinary emphasis, not a swallowed pause.
        var claimed = new[] { (1.00, 1.28), (1.28, 1.42), (1.42, 1.56) };

        Assert.Equal(1.00, Qwen3AsrAlignOnlyRunner.OnsetOf(claimed), 3);
    }

    [Fact]
    public void ALineWhoseLastCharacterSwallowedThePauseAfterItEndsAtTheSound()
    {
        // The pause after a line is absorbed by its final character, which made the cue
        // look far longer than its text takes to read - the exact signal AcceptChunk uses
        // to decide the aligner has lost track, so nearly every cue was rejected.
        var claimed = new[] { (1.00, 1.14), (1.14, 1.28), (1.28, 9.50) };

        var span = Qwen3AsrAlignOnlyRunner.SpanOf(claimed);

        Assert.Equal(1.00, span.Start, 3);
        Assert.True(span.End < 2.0, $"line ends at {span.End:F3}s, out in the silence after it");
        Assert.True(span.End >= 1.28, "the end must not move before the last character starts");
    }

    [Fact]
    public void TrimsBothEndsWhenBothSwallowedSilence()
    {
        var claimed = new[] { (0.16, 7.69), (7.69, 7.83), (7.83, 15.00) };

        var span = Qwen3AsrAlignOnlyRunner.SpanOf(claimed);

        Assert.True(span.Start > 7.0, $"starts at {span.Start:F3}s");
        Assert.True(span.End < 8.5, $"ends at {span.End:F3}s");
        Assert.True(span.End > span.Start, "the span must stay positive");
    }

    [Fact]
    public void AnOrdinaryLineKeepsBothItsTimes()
    {
        var claimed = new[] { (1.00, 1.14), (1.14, 1.26), (1.26, 1.40) };

        var span = Qwen3AsrAlignOnlyRunner.SpanOf(claimed);

        Assert.Equal(1.00, span.Start, 3);
        Assert.Equal(1.40, span.End, 3);
    }

    [Fact]
    public void ParsesTheClisJsonShape()
    {
        // Exactly what qwen3-asr-cli -o writes, trailing newline included.
        const string json = "{\"words\": [{\"word\": \"実\", \"start\": 2.362, \"end\": 2.883}, " +
                            "{\"word\": \"は\", \"start\": 2.883, \"end\": 3.303}]}\n";

        var words = Qwen3AsrAlignOnlyRunner.ParseWords(json);

        Assert.Equal(2, words.Count);
        Assert.Equal("実", words[0].Text);
        Assert.Equal(2.362, words[0].Start, 3);
        Assert.Equal(3.303, words[1].End, 3);
    }

    [Fact]
    public void HandlesEmptyAlignerOutputWithoutThrowing()
    {
        var words = Qwen3AsrAlignOnlyRunner.ParseWords("{\"words\": []}");
        Assert.Empty(words);

        var cues = ForcedAligner.ParseCues(Qwen3AsrAlignOnlyRunner.BuildSrt(new[] { "実は" }, words));
        Assert.Single(cues);
    }
}
