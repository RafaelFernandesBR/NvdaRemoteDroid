using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Lista e remove certificados confiados em modo inseguro (TOFU).</summary>
public sealed partial class TrustedCertificatesViewModel : ObservableObject
{
    private readonly ICertificateTrustStore _trustStore;

    public TrustedCertificatesViewModel(ICertificateTrustStore trustStore)
    {
        _trustStore = trustStore;
        Reload();
    }

    public ObservableCollection<TrustedCertificateEntry> Entries { get; } = new();

    [RelayCommand]
    private void Remove(TrustedCertificateEntry entry)
    {
        _trustStore.Revoke(entry.HostAndPort);
        Reload();
        SemanticScreenReader.Default.Announce(
            string.Format(Core.Resources.Strings.AppResources.TrustedCertificates_Remove, entry.HostAndPort));
    }

    private void Reload()
    {
        Entries.Clear();
        foreach (var (hostAndPort, fingerprint) in _trustStore.GetAll())
            Entries.Add(new TrustedCertificateEntry(hostAndPort, fingerprint));
    }
}

public sealed record TrustedCertificateEntry(string HostAndPort, string FingerprintSha256);
