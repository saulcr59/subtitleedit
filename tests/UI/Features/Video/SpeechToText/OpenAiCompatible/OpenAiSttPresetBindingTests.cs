using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Features.Video.SpeechToText;
using Nikse.SubtitleEdit.Features.Video.SpeechToText.OpenAiCompatible;

namespace UITests.Features.Video.SpeechToText.OpenAiCompatible;

/// <summary>
/// The preset combo and the endpoint/model fields write to each other, so the guard
/// against that turning into a loop - or into a preset silently overwriting fields the
/// user just typed - is only observable on a live view model.
/// </summary>
public class OpenAiSttPresetBindingTests : IDisposable
{
    private readonly List<Window> _windows = new();

    public void Dispose()
    {
        foreach (var window in _windows)
        {
            window.Close();
        }

        _windows.Clear();
    }

    private static SpeechToTextViewModel MakeViewModel() => new(null!, null!, null!);

    [AvaloniaFact]
    public void SelectingAPresetFillsInTheEndpointAndModel()
    {
        var vm = MakeViewModel();
        var preset = vm.OpenAiCompatibleSttPresets.Single(p => p.Model == "gpt-transcribe");

        vm.SelectedOpenAiCompatibleSttPreset = preset;

        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", vm.OpenAiCompatibleSttUrl);
        Assert.Equal("gpt-transcribe", vm.OpenAiCompatibleSttModel);
    }

    [AvaloniaFact]
    public void SelectingAPresetLeavesTheApiKeyAlone()
    {
        var vm = MakeViewModel();
        vm.OpenAiCompatibleSttApiKey = "sk-the-users-own-key";

        vm.SelectedOpenAiCompatibleSttPreset = vm.OpenAiCompatibleSttPresets.Single(p => p.Model == "whisper-1");

        Assert.Equal("sk-the-users-own-key", vm.OpenAiCompatibleSttApiKey);
    }

    [AvaloniaFact]
    public void EditingTheModelIntoSomethingUnknownFallsBackToCustom()
    {
        var vm = MakeViewModel();
        vm.SelectedOpenAiCompatibleSttPreset = vm.OpenAiCompatibleSttPresets.Single(p => p.Model == "gpt-transcribe");

        vm.OpenAiCompatibleSttModel = "some-other-model";

        Assert.NotNull(vm.SelectedOpenAiCompatibleSttPreset);
        Assert.True(vm.SelectedOpenAiCompatibleSttPreset!.IsCustom);
        // The typed value must survive the combo snapping back to Custom.
        Assert.Equal("some-other-model", vm.OpenAiCompatibleSttModel);
    }

    [AvaloniaFact]
    public void TypingSettingsThatMatchAPresetSelectsIt()
    {
        var vm = MakeViewModel();

        vm.OpenAiCompatibleSttUrl = "https://api.openai.com/v1/audio/transcriptions";
        vm.OpenAiCompatibleSttModel = "gpt-4o-transcribe";

        Assert.Equal("gpt-4o-transcribe", vm.SelectedOpenAiCompatibleSttPreset?.Model);
    }

    [AvaloniaFact]
    public void SwitchingBetweenPresetsKeepsBothFieldsConsistent()
    {
        var vm = MakeViewModel();

        foreach (var preset in vm.OpenAiCompatibleSttPresets.Where(p => !p.IsCustom))
        {
            vm.SelectedOpenAiCompatibleSttPreset = preset;

            Assert.Equal(preset.Url, vm.OpenAiCompatibleSttUrl);
            Assert.Equal(preset.Model, vm.OpenAiCompatibleSttModel);
            Assert.Same(preset, vm.SelectedOpenAiCompatibleSttPreset);
        }
    }

    [AvaloniaFact]
    public void SelectingCustomDoesNotWipeTheCurrentSettings()
    {
        var vm = MakeViewModel();
        vm.SelectedOpenAiCompatibleSttPreset = vm.OpenAiCompatibleSttPresets.Single(p => p.Model == "gpt-transcribe");

        vm.SelectedOpenAiCompatibleSttPreset = vm.OpenAiCompatibleSttPresets.First(p => p.IsCustom);

        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", vm.OpenAiCompatibleSttUrl);
        Assert.Equal("gpt-transcribe", vm.OpenAiCompatibleSttModel);
    }

    [AvaloniaFact]
    public void TheSpeechToTextWindowStillBuilds()
    {
        // The preset row is added to a hand-built control tree; a mistake there only
        // shows up when the window is actually constructed.
        var vm = MakeViewModel();

        var window = new SpeechToTextWindow(vm);
        _windows.Add(window);

        Assert.NotNull(window);
        Assert.NotEmpty(vm.OpenAiCompatibleSttPresets);
    }
}
