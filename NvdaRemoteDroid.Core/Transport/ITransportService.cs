using NvdaRemoteDroid.Core.Protocol.Messages;

namespace NvdaRemoteDroid.Core.Transport;

/// <summary>
/// Abstrai o transporte TCP/TLS com um relay NVDA Remote. A implementação concreta
/// (TcpTlsTransportService, no projeto Android) cuida de socket, TLS, TOFU, framing e
/// reconexão; esta interface permite que ViewModels do Core sejam testadas sem rede real.
/// </summary>
public interface ITransportService : IAsyncDisposable
{
    ConnectionState State { get; }

    event EventHandler<ConnectionState>? StateChanged;
    event EventHandler<IRemoteMessage>? MessageReceived;

    /// <summary>Disparado quando, em modo inseguro, o certificado do host ainda não é confiável — aguarda TrustCurrentCertificateAsync ou Disconnect.</summary>
    event EventHandler<string>? CertificateTrustRequested;

    Task ConnectAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    Task SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default);

    /// <summary>Confirma o fingerprint pendente (de CertificateTrustRequested) e retoma o handshake.</summary>
    Task TrustCurrentCertificateAsync();
}
