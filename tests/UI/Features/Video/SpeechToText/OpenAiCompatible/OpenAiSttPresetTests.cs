using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;

namespace UITests.Features.Video.SpeechToText.OpenAiCompatible;

public class OpenAiSttPresetTests
{
    [Fact]
    public void FirstEntryIsCustomAndChangesNothing()
    {
        var presets = OpenAiSttPreset.All();

        Assert.True(presets[0].IsCustom);
        Assert.Empty(presets[0].Url);
        Assert.Empty(presets[0].Model);
    }

    [Fact]
    public void GptTranscribePresetPointsAtOpenAi()
    {
        var preset = Assert.Single(OpenAiSttPreset.All(), p => p.Model == "gpt-transcribe");

        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", preset.Url);
        Assert.False(preset.IsCustom);
    }

    [Fact]
    public void EveryNonCustomPresetHasBothFields()
    {
        foreach (var preset in OpenAiSttPreset.All())
        {
            if (preset.IsCustom)
            {
                continue;
            }

            Assert.False(string.IsNullOrWhiteSpace(preset.Url), $"{preset.Display} has no URL");
            Assert.False(string.IsNullOrWhiteSpace(preset.Model), $"{preset.Display} has no model");
        }
    }

    [Fact]
    public void MatchFindsThePresetForTheCurrentSettings()
    {
        var presets = OpenAiSttPreset.All();

        var match = OpenAiSttPreset.Match(
            presets, "https://api.openai.com/v1/audio/transcriptions", "gpt-transcribe");

        Assert.Equal("gpt-transcribe", match.Model);
    }

    [Fact]
    public void MatchIgnoresSurroundingWhitespaceAndCase()
    {
        var presets = OpenAiSttPreset.All();

        var match = OpenAiSttPreset.Match(
            presets, "  HTTPS://API.OPENAI.COM/v1/audio/transcriptions ", " GPT-Transcribe ");

        Assert.Equal("gpt-transcribe", match.Model);
    }

    [Fact]
    public void MatchFallsBackToCustomForAnUnknownServer()
    {
        var presets = OpenAiSttPreset.All();

        Assert.True(OpenAiSttPreset.Match(presets, "https://example.com/v1/stt", "whatever").IsCustom);
        Assert.True(OpenAiSttPreset.Match(presets, null, null).IsCustom);
    }

    [Fact]
    public void SameEndpointWithADifferentModelIsADifferentPreset()
    {
        // Every OpenAI preset shares one endpoint, so matching on URL alone would
        // always return whichever came first in the list.
        var presets = OpenAiSttPreset.All();
        const string url = "https://api.openai.com/v1/audio/transcriptions";

        Assert.Equal("whisper-1", OpenAiSttPreset.Match(presets, url, "whisper-1").Model);
        Assert.Equal("gpt-4o-transcribe", OpenAiSttPreset.Match(presets, url, "gpt-4o-transcribe").Model);
    }
}
