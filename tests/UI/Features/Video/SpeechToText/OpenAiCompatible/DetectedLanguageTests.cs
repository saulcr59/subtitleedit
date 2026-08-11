using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;
using System.Text.Json;

namespace UITests.Features.Video.SpeechToText.OpenAiCompatible;

/// <summary>
/// Providers disagree on how the detected language is reported, and the transcript's
/// language decides which forced aligner is used, so missing it is not cosmetic.
/// </summary>
public class DetectedLanguageTests
{
    private static OpenAiCompatibleSttResponse Parse(string json)
        => JsonSerializer.Deserialize<OpenAiCompatibleSttResponse>(json)!;

    [Fact]
    public void ReadsThePluralLanguagesArrayGptTranscribeSends()
    {
        // Verbatim from api.openai.com with model=gpt-transcribe.
        const string json = "{\"text\":\"実は、フレンドから\",\"languages\":[{\"code\":\"ja\"}]," +
                            "\"usage\":{\"type\":\"duration\",\"seconds\":20}}";

        Assert.Equal("ja", Parse(json).EffectiveLanguage);
    }

    [Fact]
    public void StillReadsTheSingularLanguageKey()
    {
        Assert.Equal("english", Parse("{\"text\":\"hi\",\"language\":\"english\"}").EffectiveLanguage);
    }

    [Fact]
    public void PrefersTheSingularKeyWhenBothArePresent()
    {
        const string json = "{\"text\":\"hi\",\"language\":\"en\",\"languages\":[{\"code\":\"ja\"}]}";

        Assert.Equal("en", Parse(json).EffectiveLanguage);
    }

    [Fact]
    public void ReportsNothingWhenNoLanguageIsGiven()
    {
        Assert.Null(Parse("{\"text\":\"hi\"}").EffectiveLanguage);
        Assert.Null(Parse("{\"text\":\"hi\",\"languages\":[]}").EffectiveLanguage);
        Assert.Null(Parse("{\"text\":\"hi\",\"languages\":[{\"code\":\"\"}]}").EffectiveLanguage);
    }

    [Fact]
    public void SkipsBlankEntriesInTheArray()
    {
        const string json = "{\"text\":\"hi\",\"languages\":[{\"code\":null},{\"code\":\"ja\"}]}";

        Assert.Equal("ja", Parse(json).EffectiveLanguage);
    }
}
