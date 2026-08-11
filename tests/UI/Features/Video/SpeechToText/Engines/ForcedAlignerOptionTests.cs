using Nikse.SubtitleEdit.Features.Video.SpeechToText.Engines;

namespace UITests.Features.Video.SpeechToText.Engines;

/// <summary>
/// Guards the choice-string -> option resolution. An unrecognised choice falls back to
/// the built-in aligner silently, so a typo or a code-collapse mistake in
/// ExtractLanguageCode would quietly downgrade the aligner instead of failing loudly.
/// </summary>
public class ForcedAlignerOptionTests
{
    [Fact]
    public void IvydataChoiceResolvesToTheIvydataAligner()
    {
        var option = ForcedAlignerOption.FromChoice(ForcedAlignerOption.Wav2Vec2JaIvydataChoice);

        Assert.False(option.IsBuiltIn);
        Assert.Equal(ForcedAlignerOption.Wav2Vec2JaIvydataChoice, option.Choice);
        Assert.Equal("wav2vec2-ctc-ja-ivydata-f16.gguf", option.FileName);
    }

    [Fact]
    public void PlainJapaneseChoiceIsNotShadowedByTheIvydataEntry()
    {
        var option = ForcedAlignerOption.FromChoice(ForcedAlignerOption.Wav2Vec2JaChoice);

        Assert.Equal(ForcedAlignerOption.Wav2Vec2JaChoice, option.Choice);
        Assert.Equal("wav2vec2-large-xlsr-53-japanese-q4_k.gguf", option.FileName);
    }

    [Fact]
    public void EveryOfferedAlignerRoundTripsThroughItsChoice()
    {
        foreach (var option in ForcedAlignerOption.Wav2Vec2All())
        {
            var resolved = ForcedAlignerOption.FromChoice(option.Choice);

            Assert.False(resolved.IsBuiltIn, $"{option.Choice} fell back to the built-in aligner");
            Assert.Equal(option.Choice, resolved.Choice);
            Assert.Equal(option.FileName, resolved.FileName);
        }
    }

    [Fact]
    public void AlignerFileNamesAreDistinct()
    {
        // Two aligners sharing a file name would overwrite each other in the models
        // folder, and the second would silently run on the first one's weights.
        var fileNames = ForcedAlignerOption.All()
            .Where(o => !string.IsNullOrEmpty(o.FileName))
            .Select(o => o.FileName)
            .ToList();

        Assert.Equal(fileNames.Count, fileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void LocallySuppliedAlignerExposesNoDownloadUrl()
    {
        // The Ivydata GGUF is converted locally rather than hosted, so it must not
        // advertise a URL - the download list skips URL-less entries and the download
        // itself would have nothing to fetch.
        var option = ForcedAlignerOption.FromChoice(ForcedAlignerOption.Wav2Vec2JaIvydataChoice);

        Assert.Empty(option.Url);
        Assert.Empty(option.ToWhisperModel().Urls);
    }
}
