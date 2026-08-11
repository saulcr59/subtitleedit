using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;

namespace UITests.Features.Video.SpeechToText.OpenAiCompatible;

/// <summary>
/// verbose_json is the only response format that carries timestamps, but asking for it
/// where it is not supported fails the whole request with a 400 rather than degrading.
/// </summary>
public class VerboseJsonSupportTests
{
    [Theory]
    [InlineData("gpt-transcribe")]
    [InlineData("gpt-4o-transcribe")]
    [InlineData("gpt-4o-mini-transcribe")]
    [InlineData("GPT-Transcribe")]
    [InlineData("  gpt-transcribe  ")]
    public void GptTranscribeFamilyIsSentAsPlainJson(string model)
    {
        Assert.False(OpenAiSttService.SupportsVerboseJson(model));
    }

    [Theory]
    [InlineData("whisper-1")]
    [InlineData("whisper-large-v3")]
    [InlineData("Systran/faster-whisper-large-v3")]
    public void WhisperModelsKeepVerboseJson(string model)
    {
        Assert.True(OpenAiSttService.SupportsVerboseJson(model));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("some-unknown-server-model")]
    public void UnknownAndEmptyModelsKeepVerboseJson(string? model)
    {
        // This engine talks to arbitrary OpenAI-compatible servers; downgrading by
        // default would throw away timestamps they were willing to provide.
        Assert.True(OpenAiSttService.SupportsVerboseJson(model));
    }

    [Fact]
    public void AChatModelNamedGptIsNotMistakenForATranscribeModel()
    {
        Assert.True(OpenAiSttService.SupportsVerboseJson("gpt-4o"));
        Assert.True(OpenAiSttService.SupportsVerboseJson("gpt-5"));
    }
}
