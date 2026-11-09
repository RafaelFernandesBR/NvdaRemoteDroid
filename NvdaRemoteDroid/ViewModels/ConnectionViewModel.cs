using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela de Conexão: anúncio por leitor de tela e navegação via Shell.</summary>
public sealed class ConnectionViewModel : ConnectionViewModelBase
{
    public ConnectionViewModel(ITransportService transportService, IConnectionPreferencesService preferencesService)
        : base(transportService, preferencesService)
    {
    }

    protected override void AnnounceStatus(string statusText) =>
        MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Default.Announce(statusText));

    protected override async Task<bool> ConfirmCertificateTrustAsync(string fingerprintSha256)
    {
        var message = string.Format(Core.Resources.Strings.AppResources.CertificateTrust_Message, fingerprintSha256);

        return await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.DisplayAlertAsync(
                Core.Resources.Strings.AppResources.CertificateTrust_Title,
                message,
                Core.Resources.Strings.AppResources.CertificateTrust_Accept,
                Core.Resources.Strings.AppResources.CertificateTrust_Reject));
    }

    protected override void OnAuthenticated() =>
        MainThread.BeginInvokeOnMainThread(() => Shell.Current.GoToAsync($"//{nameof(RemoteControlPage)}"));
}
