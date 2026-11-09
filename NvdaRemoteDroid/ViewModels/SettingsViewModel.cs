using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela de Configurações.</summary>
public sealed class SettingsViewModel : SettingsViewModelBase
{
    public SettingsViewModel(IConnectionPreferencesService preferencesService) : base(preferencesService)
    {
    }
}
