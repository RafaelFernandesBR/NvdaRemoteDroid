using CommunityToolkit.Mvvm.ComponentModel;
using NvdaRemoteDroid.Core.Gateway;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>Lógica da tela de Configurações: tecla NVDA preferida, voz/velocidade do TTS, idioma.</summary>
public abstract partial class SettingsViewModelBase : ObservableObject
{
    private readonly IConnectionPreferencesService _preferencesService;

    protected SettingsViewModelBase(IConnectionPreferencesService preferencesService)
    {
        _preferencesService = preferencesService;
        nvdaKey = _preferencesService.NvdaKey;
    }

    [ObservableProperty]
    private VirtualKeyNvdaChoice nvdaKey;

    partial void OnNvdaKeyChanged(VirtualKeyNvdaChoice value) => _preferencesService.NvdaKey = value;
}
