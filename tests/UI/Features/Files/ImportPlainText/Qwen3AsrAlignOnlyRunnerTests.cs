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
