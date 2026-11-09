using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>
/// Lógica da tela de Conexão, isolada de MAUI/Android: decide host/porta (direto ou relay
/// público) e chave de canal, inicia a conexão e reage às transições de estado. Subclasses de
/// plataforma implementam os métodos abstratos que tocam a UI real (anúncio por leitor de
/// tela, confirmação do prompt de TOFU, navegação após conectar).
/// </summary>
public abstract partial class ConnectionViewModelBase : ObservableObject
{
    private readonly ITransportService _transportService;
    private readonly IConnectionPreferencesService _preferencesService;

    protected ConnectionViewModelBase(ITransportService transportService, IConnectionPreferencesService preferencesService)
    {
        _transportService = transportService;
        _preferencesService = preferencesService;

        _transportService.StateChanged += OnTransportStateChanged;
        _transportService.CertificateTrustRequested += OnCertificateTrustRequested;

        RecentConnections = _preferencesService.GetRecentConnections();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool useRelay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string host = string.Empty;

    [ObservableProperty]
    private int port = RemoteDefaults.Port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string channel = string.Empty;

    [ObservableProperty]
    private bool insecureMode;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConnect))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool isBusy;

    public IReadOnlyList<ConnectionInfo> RecentConnections { get; private set; }

    public bool CanConnect => !IsBusy && !string.IsNullOrWhiteSpace(Channel) && (UseRelay || !string.IsNullOrWhiteSpace(Host));

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (!CanConnect)
            return;

        IsBusy = true;
        try
        {
            var connectionInfo = UseRelay
                ? ConnectionInfo.PublicRelay(Channel, InsecureMode)
                : new ConnectionInfo(Host, Port, Channel, InsecureMode);

            _preferencesService.SaveRecentConnection(connectionInfo);
            RecentConnections = _preferencesService.GetRecentConnections();
            OnPropertyChanged(nameof(RecentConnections));

            await _transportService.ConnectAsync(connectionInfo);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task UseRecentConnectionAsync(ConnectionInfo connectionInfo)
    {
        UseRelay = connectionInfo.Host == RemoteDefaults.PublicRelayHost;
        Host = connectionInfo.Host;
        Port = connectionInfo.Port;
        Channel = connectionInfo.Channel;
        InsecureMode = connectionInfo.Insecure;
        return ConnectAsync();
    }

    private void OnTransportStateChanged(object? sender, ConnectionState state)
    {
        StatusText = DescribeState(state);
        AnnounceStatus(StatusText);

        if (state == ConnectionState.Authenticated)
            OnAuthenticated();
    }

    private async void OnCertificateTrustRequested(object? sender, string fingerprint)
    {
        var trusted = await ConfirmCertificateTrustAsync(fingerprint);
        if (trusted)
            await _transportService.TrustCurrentCertificateAsync();
        else
            await _transportService.DisconnectAsync();
    }

    private static string DescribeState(ConnectionState state) => state switch
    {
        ConnectionState.Disconnected => "Desconectado",
        ConnectionState.Connecting => "Conectando...",
        ConnectionState.AwaitingCertificateTrust => "Aguardando confirmação do certificado...",
        ConnectionState.Handshaking => "Autenticando...",
        ConnectionState.Authenticated => "Conectado",
        ConnectionState.AuthFailed => "Senha do canal incorreta",
        ConnectionState.VersionMismatch => "Versão do protocolo incompatível",
        ConnectionState.Error => "Falha de conexão, tentando novamente...",
        _ => state.ToString(),
    };

    /// <summary>Anuncia a mudança de status via leitor de tela (SemanticScreenReader no Android).</summary>
    protected abstract void AnnounceStatus(string statusText);

    /// <summary>Pede confirmação ao usuário sobre o fingerprint do certificado (TOFU), retorna true se confiar.</summary>
    protected abstract Task<bool> ConfirmCertificateTrustAsync(string fingerprintSha256);

    /// <summary>Chamado quando a conexão é autenticada com sucesso (ex. navegar para a tela de controle).</summary>
    protected abstract void OnAuthenticated();
}
