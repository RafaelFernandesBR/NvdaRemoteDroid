namespace NvdaRemoteDroid.Core.Transport;

/// <summary>
/// Mapeamento compartilhado de <see cref="ConnectionState"/> para texto de status e para o
/// booleano que decide se a UI mostra "Conectar" ou "Desconectar"/trava o formulário — usado
/// por ConnectionViewModelBase e RemoteControlViewModelBase para não duplicar (e dessincronizar)
/// essa lógica entre as duas telas.
/// </summary>
public static class ConnectionStateDescriber
{
    public static string Describe(ConnectionState state) => state switch
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

    /// <summary>True enquanto conectando, autenticando ou já conectado (inclui Error, que está
    /// em backoff de reconexão automática) — o único jeito de saído dali é Desconectar.</summary>
    public static bool IsConnectedOrConnecting(ConnectionState state) => state is ConnectionState.Connecting
        or ConnectionState.AwaitingCertificateTrust
        or ConnectionState.Handshaking
        or ConnectionState.Authenticated
        or ConnectionState.Error;
}
