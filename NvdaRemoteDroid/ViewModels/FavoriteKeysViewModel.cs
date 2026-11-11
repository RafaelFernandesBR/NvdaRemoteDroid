using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela "Teclas favoritas".</summary>
public sealed class FavoriteKeysViewModel : FavoriteKeysViewModelBase
{
    public FavoriteKeysViewModel(IConnectionPreferencesService preferencesService) : base(preferencesService)
    {
    }

    protected override void Announce(string message) => SemanticScreenReader.Default.Announce(message);
}
