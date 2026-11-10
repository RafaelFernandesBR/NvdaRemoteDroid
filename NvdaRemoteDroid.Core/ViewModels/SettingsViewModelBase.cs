using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Speech;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>Uma opção do seletor de voz (Key vazia = padrão do dispositivo).</summary>
public sealed record TtsEngineOption(string Key, string Display);

/// <summary>Lógica da tela de Configurações: tecla NVDA preferida, motor/velocidade do TTS, idioma.</summary>
public abstract partial class SettingsViewModelBase : ObservableObject
{
    private readonly IConnectionPreferencesService _preferencesService;
    private readonly ISpeechOutputService _speechOutputService;
    private readonly bool _initialized;

    protected SettingsViewModelBase(IConnectionPreferencesService preferencesService, ISpeechOutputService speechOutputService)
    {
        _preferencesService = preferencesService;
        _speechOutputService = speechOutputService;
        nvdaKey = _preferencesService.NvdaKey;

        // Primeira opção: manter o motor padrão do dispositivo.
        var defaultOption = new TtsEngineOption(string.Empty, AppResources.Settings_Voice_DeviceDefault);
        Engines.Add(defaultOption);
        foreach (var engine in _speechOutputService.GetInstalledEngines())
            Engines.Add(new TtsEngineOption(engine.Id, engine.Display));

        var savedKey = _preferencesService.TtsEnginePackage ?? string.Empty;
        var saved = Engines.FirstOrDefault(e => e.Key == savedKey);
        selectedEngine = saved ?? defaultOption;

        speechRate = SpeechRateLimits.Clamp(_preferencesService.SpeechRate);
        UpdateSpeechRateLabel();

        _initialized = true;

        // Motor salvo desinstalado: normaliza para o padrão, alinhando preferência e serviço
        // (senão "Padrão" não dispararia OnSelectedEngineChanged por já estar selecionado).
        if (saved is null && savedKey.Length > 0)
        {
            _preferencesService.TtsEnginePackage = string.Empty;
            _speechOutputService.Configure(string.Empty, (float)speechRate);
        }
    }

    public ObservableCollection<TtsEngineOption> Engines { get; } = new();

    [ObservableProperty]
    private VirtualKeyNvdaChoice nvdaKey;

    [ObservableProperty]
    private TtsEngineOption? selectedEngine;

    [ObservableProperty]
    private double speechRate;

    [ObservableProperty]
    private string speechRateLabel = string.Empty;

    /// <summary>Anuncia uma mudança de estado ao leitor de tela (implementado na plataforma).</summary>
    protected virtual void Announce(string message) { }

    partial void OnNvdaKeyChanged(VirtualKeyNvdaChoice value) => _preferencesService.NvdaKey = value;

    partial void OnSelectedEngineChanged(TtsEngineOption? value)
    {
        if (!_initialized || value is null)
            return;

        _preferencesService.TtsEnginePackage = value.Key;
        _speechOutputService.Configure(value.Key, SpeechRateLimits.Clamp((float)SpeechRate));
        Announce(string.Format(AppResources.Settings_Voice_Selected, value.Display));
    }

    partial void OnSpeechRateChanged(double value)
    {
        // Passo de 0,1x e limites 0,5–2,0; se o valor foi ajustado, reatribui (re-entra uma vez).
        var normalized = Math.Round(SpeechRateLimits.Clamp((float)value), 1);
        if (Math.Abs(normalized - value) > 1e-9)
        {
            SpeechRate = normalized;
            return;
        }

        UpdateSpeechRateLabel();

        if (!_initialized)
            return;

        _preferencesService.SpeechRate = (float)normalized;
        _speechOutputService.Configure(SelectedEngine?.Key, (float)normalized);
    }

    private void UpdateSpeechRateLabel() =>
        SpeechRateLabel = string.Format(
            AppResources.Settings_SpeechRate_Label,
            SpeechRate.ToString("0.0", CultureInfo.CurrentUICulture));

    [RelayCommand]
    private void TestVoice() => _speechOutputService.SpeakSample(AppResources.Settings_Voice_SampleText);
}
