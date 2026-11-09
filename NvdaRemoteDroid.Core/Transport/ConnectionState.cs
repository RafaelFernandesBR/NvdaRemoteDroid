namespace NvdaRemoteDroid.Core.Transport;

/// <summary>
/// Estado de uma conexão com o relay NVDA Remote. AuthFailed e VersionMismatch são terminais:
/// nenhuma reconexão automática deve ser disparada a partir deles (evita martelar o relay com
/// senha errada) — apenas Error (falha de IO/transporte) alimenta o backoff de reconexão.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    AwaitingCertificateTrust,
    Handshaking,
    Authenticated,
    AuthFailed,
    VersionMismatch,
    Error,
}
