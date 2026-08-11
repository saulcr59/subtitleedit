using Nikse.SubtitleEdit.Logic.Config;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;

/// <summary>
/// A known endpoint/model pairing for the OpenAI Compatible Server engine, so the
/// common providers do not have to be typed in by hand. Only the API key is ever
/// user-specific, so that is deliberately not part of a preset.
/// </summary>
public sealed class OpenAiSttPreset
{
    public string Display { get; }
    public string Url { get; }
    public string Model { get; }

    /// <summary>The "Custom" entry, which leaves the fields exactly as they are.</summary>
    public bool IsCustom => string.IsNullOrEmpty(Url);

    public OpenAiSttPreset(string display, string url, string model)
    {
        Display = display;
        Url = url;
        Model = model;
    }

    public override string ToString() => Display;

    private const string OpenAiUrl = "https://api.openai.com/v1/audio/transcriptions";

    public static IReadOnlyList<OpenAiSttPreset> All() => new[]
    {
        new OpenAiSttPreset(Se.Language.General.OpenAiCompatibleSttPresetCustom, string.Empty, string.Empty),

        // Text only - it rejects verbose_json outright - but the best transcription of
        // the three. The missing time codes are recovered by forced alignment after the
        // fact, which is why this is a usable default rather than a trade-off.
        new OpenAiSttPreset("OpenAI - gpt-transcribe (best text, timed by aligner)", OpenAiUrl, "gpt-transcribe"),

        new OpenAiSttPreset("OpenAI - gpt-4o-transcribe", OpenAiUrl, "gpt-4o-transcribe"),
        new OpenAiSttPreset("OpenAI - gpt-4o-mini-transcribe", OpenAiUrl, "gpt-4o-mini-transcribe"),
        new OpenAiSttPreset("OpenAI - whisper-1 (own timestamps)", OpenAiUrl, "whisper-1"),
    };

    /// <summary>
    /// The preset matching the current settings, or the Custom entry when nothing matches.
    /// Compared on endpoint and model together, since the same endpoint serves several models.
    /// </summary>
    public static OpenAiSttPreset Match(IReadOnlyList<OpenAiSttPreset> presets, string? url, string? model)
    {
        foreach (var preset in presets)
        {
            if (preset.IsCustom)
            {
                continue;
            }

            if (string.Equals(preset.Url, url?.Trim(), System.StringComparison.OrdinalIgnoreCase) &&
                string.Equals(preset.Model, model?.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return presets[0];
    }
}
