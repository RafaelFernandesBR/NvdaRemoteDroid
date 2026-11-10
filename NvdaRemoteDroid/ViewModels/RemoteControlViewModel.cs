using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Session;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela de Controle Remoto.</summary>
public sealed class RemoteControlViewModel : RemoteControlViewModelBase
{
    public RemoteControlViewModel(
        ITransportService transportService,
        IClipboardService clipboardService,
        IRemoteSessionCoordinator sessionCoordinator)
        : base(transportService, clipboardService, sessionCoordinator)
    {
    }

    protected override void AnnounceStatus(string text) =>
        MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Default.Announce(text));

    protected override void NavigateToConnection() =>
        MainThread.BeginInvokeOnMainThread(() => _ = Shell.Current.GoToAsync($"//{nameof(ConnectionPage)}"));
}
