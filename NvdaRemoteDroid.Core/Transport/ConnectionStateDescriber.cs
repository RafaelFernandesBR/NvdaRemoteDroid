using NvdaRemoteDroid.Core.Resources.Strings;

namespace NvdaRemoteDroid.Core.Transport;

/// <summary>
/// Mapeamento compartilhado de <see cref="ConnectionState"/> para texto de status (localizado)
/// e para o booleano que decide se a UI mostra "Conectar" ou "Desconectar"/trava o formulário —
/// usado pelas telas e pela notificação do serviço em primeiro plano para não duplicar (e
/// dessincronizar) essa lógica.
/// </summary>
public static class ConnectionStateDescriber
{
    public static string Describe(ConnectionState state) => state switch
    {
        ConnectionState.Disconnected => AppResources.Status_Disconnected,
        ConnectionState.Connecting => AppResources.Status_Connecting,
        ConnectionState.AwaitingCertificateTrust => AppResources.Status_AwaitingCertificateTrust,
        ConnectionState.Handshaking => AppResources.Status_Handshaking,
        ConnectionState.Authenticated => AppResources.Status_Authenticated,
        ConnectionState.AuthFailed => AppResources.Status_AuthFailed,
        ConnectionState.VersionMismatch => AppResources.Status_VersionMismatch,
        ConnectionState.Error => AppResources.Status_Error,
        _ => state.ToString(),
    };

    /// <summary>True enquanto conectando, autenticando ou já conectado (inclui Error, que está
    /// em backoff de reconexão automática) — o único jeito de sair dali é Desconectar.</summary>
    public static bool IsConnectedOrConnecting(ConnectionState state) => state is ConnectionState.Connecting
        or ConnectionState.AwaitingCertificateTrust
        or ConnectionState.Handshaking
        or ConnectionState.Authenticated
        or ConnectionState.Error;
}
