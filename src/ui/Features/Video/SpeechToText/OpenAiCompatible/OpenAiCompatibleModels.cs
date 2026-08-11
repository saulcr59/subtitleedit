using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;

public class OpenAiCompatibleSttResponse
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("segments")]
    public List<OpenAiCompatibleSegment>? Segments { get; set; }

    /// <summary>
    /// Top-level word timings. Some OpenAI-compatible providers (e.g. xAI Grok
    /// at /v1/stt) return only this array — with per-word start/end and no
    /// <see cref="Segments"/> — so the timings must be grouped into segments
    /// instead of being dropped (discussion #11239).
    /// </summary>
    [JsonPropertyName("words")]
    public List<OpenAiCompatibleWord>? Words { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// Detected languages under a plural key. OpenAI's gpt-transcribe reports
    /// <c>"languages": [{"code": "ja"}]</c> and never sends the singular
    /// <see cref="Language"/>, so without this its transcripts arrive with no
    /// language attached at all.
    /// </summary>
    [JsonPropertyName("languages")]
    public List<OpenAiCompatibleDetectedLanguage>? Languages { get; set; }

    /// <summary>
    /// The reported language whichever key the provider used, or null.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveLanguage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Language))
            {
                return Language;
            }

            if (Languages != null)
            {
                foreach (var language in Languages)
                {
                    if (!string.IsNullOrWhiteSpace(language.Code))
                    {
                        return language.Code;
                    }
                }
            }

            return null;
        }
    }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}

public class OpenAiCompatibleDetectedLanguage
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class OpenAiCompatibleSegment
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("seek")]
    public int Seek { get; set; }

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("tokens")]
    public List<int>? Tokens { get; set; }

    [JsonPropertyName("avg_logprob")]
    public double AvgLogprob { get; set; }

    [JsonPropertyName("no_speech_prob")]
    public double NoSpeechProb { get; set; }

    [JsonPropertyName("words")]
    public List<OpenAiCompatibleWord>? Words { get; set; }
}

public class OpenAiCompatibleWord
{
    [JsonPropertyName("word")]
    public string Word { get; set; } = string.Empty;

    /// <summary>
    /// Word text under the "text" key. OpenAI's verbose_json uses "word"; xAI
    /// Grok uses "text". Whichever the provider sends, the other stays empty.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public double Start { get; set; }

    [JsonPropertyName("end")]
    public double End { get; set; }

    [JsonPropertyName("probability")]
    public double Probability { get; set; }

    /// <summary>
    /// The word text regardless of which key the provider used ("word" or "text").
    /// </summary>
    [JsonIgnore]
    public string EffectiveText => !string.IsNullOrEmpty(Word) ? Word : Text;
}
