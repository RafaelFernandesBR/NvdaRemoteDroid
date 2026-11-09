namespace NvdaRemoteDroid.Core.Transport;

/// <summary>Evento que provoca uma transição de estado da conexão.</summary>
public enum ConnectionEvent
{
    ConnectRequested,
    UntrustedCertificateEncountered,
    CertificateTrusted,
    TlsReady,
    ChannelJoined,
    IncorrectPassword,
    VersionMismatch,
    TransportFailed,
    DisconnectRequested,
}

/// <summary>
/// Transições puras de ConnectionState, sem nenhuma dependência de rede — testável por tabela.
/// AuthFailed e VersionMismatch são terminais: só DisconnectRequested ou ConnectRequested (nova
/// tentativa manual) saem deles; TransportFailed nunca os sucede automaticamente.
/// </summary>
public sealed class RemoteConnectionStateMachine
{
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public ConnectionState Apply(ConnectionEvent connectionEvent)
    {
        State = (State, connectionEvent) switch
        {
            (_, ConnectionEvent.ConnectRequested) => ConnectionState.Connecting,

            (ConnectionState.Connecting, ConnectionEvent.UntrustedCertificateEncountered) => ConnectionState.AwaitingCertificateTrust,
            (ConnectionState.AwaitingCertificateTrust, ConnectionEvent.CertificateTrusted) => ConnectionState.Connecting,

            (ConnectionState.Connecting, ConnectionEvent.TlsReady) => ConnectionState.Handshaking,
            (ConnectionState.Handshaking, ConnectionEvent.ChannelJoined) => ConnectionState.Authenticated,
            (ConnectionState.Handshaking, ConnectionEvent.IncorrectPassword) => ConnectionState.AuthFailed,
            (ConnectionState.Handshaking, ConnectionEvent.VersionMismatch) => ConnectionState.VersionMismatch,

            // Falha de transporte só reconecta a partir de estados não-terminais.
            (ConnectionState.Connecting, ConnectionEvent.TransportFailed) => ConnectionState.Error,
            (ConnectionState.Handshaking, ConnectionEvent.TransportFailed) => ConnectionState.Error,
            (ConnectionState.Authenticated, ConnectionEvent.TransportFailed) => ConnectionState.Error,
            (ConnectionState.AuthFailed, ConnectionEvent.TransportFailed) => ConnectionState.AuthFailed,
            (ConnectionState.VersionMismatch, ConnectionEvent.TransportFailed) => ConnectionState.VersionMismatch,

            (_, ConnectionEvent.DisconnectRequested) => ConnectionState.Disconnected,

            _ => State,
        };

        return State;
    }

    /// <summary>Indica se o estado atual deve alimentar o backoff automático de 5s do transporte.</summary>
    public bool ShouldAutoReconnect => State == ConnectionState.Error;
}
