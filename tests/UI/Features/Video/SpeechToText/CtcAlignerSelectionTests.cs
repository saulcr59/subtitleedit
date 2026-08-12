using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;

namespace UITests.Features.Video.SpeechToText;

/// <summary>
/// Several wav2vec2 aligners can serve one language, and picking the wrong one is
/// invisible: alignment still succeeds, just with the model that was replaced.
/// </summary>
public class CtcAlignerSelectionTests
{
    private const string Prefix = "wav2vec2-aligner-";

    /// <summary>Mirrors the choice-preference rule used to pick an installed aligner.</summary>
    private static string? Pick(IEnumerable<string> installedChoices, string languageCode)
        => installedChoices
            .Where(c => c.StartsWith(Prefix + languageCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Length)
            .FirstOrDefault();

    [Fact]
    public void PrefersTheMoreSpecificAlignerForALanguage()
    {
        var installed = new[]
        {
            ForcedAlignerOption.Wav2Vec2JaChoice,
            ForcedAlignerOption.Wav2Vec2JaIvydataChoice,
        };

        Assert.Equal(ForcedAlignerOption.Wav2Vec2JaIvydataChoice, Pick(installed, "ja"));
    }

    [Fact]
    public void OrderOfInstallationDoesNotDecide()
    {
        var installed = new[]
        {
            ForcedAlignerOption.Wav2Vec2JaIvydataChoice,
            ForcedAlignerOption.Wav2Vec2JaChoice,
        };

        Assert.Equal(ForcedAlignerOption.Wav2Vec2JaIvydataChoice, Pick(installed, "ja"));
    }

    [Fact]
    public void FallsBackToTheGenericEntryWhenItIsTheOnlyOne()
    {
        var installed = new[] { ForcedAlignerOption.Wav2Vec2JaChoice };

        Assert.Equal(ForcedAlignerOption.Wav2Vec2JaChoice, Pick(installed, "ja"));
    }

    [Fact]
    public void DoesNotPickAnotherLanguage()
    {
        var installed = new[]
        {
            ForcedAlignerOption.Wav2Vec2EnChoice,
            ForcedAlignerOption.Wav2Vec2DeChoice,
        };

        Assert.Null(Pick(installed, "ja"));
    }

    [Fact]
    public void TheIvydataChoiceIsActuallyLongerThanTheGenericOne()
    {
        // The preference rule rests on this, so it should fail here rather than in the
        // pipeline if either constant is ever renamed.
        Assert.StartsWith(ForcedAlignerOption.Wav2Vec2JaChoice, ForcedAlignerOption.Wav2Vec2JaIvydataChoice);
        Assert.True(ForcedAlignerOption.Wav2Vec2JaIvydataChoice.Length > ForcedAlignerOption.Wav2Vec2JaChoice.Length);
    }
}
