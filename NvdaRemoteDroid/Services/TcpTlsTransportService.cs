using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using NvdaRemoteDroid.Core.Protocol;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// Transporte real do protocolo NVDA Remote: TcpClient + SslStream (TLS 1.2), framing por
/// linha via LineFramer, handshake protocol_version -> join, TOFU por fingerprint SHA-256 em
/// modo inseguro, fila de envio serializada e reconexão com backoff fixo de 5s — mas só a
/// partir de falhas de transporte, nunca de AuthFailed/VersionMismatch (ver RemoteConnectionStateMachine).
/// </summary>
public sealed class TcpTlsTransportService : ITransportService
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ICertificateTrustStore _trustStore;
    private readonly RemoteConnectionStateMachine _stateMachine = new();

    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private Channel<string>? _sendQueue;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _runLoopTask;

    private ConnectionInfo? _connectionInfo;
    private string? _pendingFingerprint;
    private TaskCompletionSource<bool>? _trustDecisionSource;

    public TcpTlsTransportService(ICertificateTrustStore trustStore)
    {
        _trustStore = trustStore;
    }

    public ConnectionState State => _stateMachine.State;

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<IRemoteMessage>? MessageReceived;
    public event EventHandler<string>? CertificateTrustRequested;

    public Task ConnectAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        _connectionInfo = connectionInfo;
        _lifetimeCts = new CancellationTokenSource();
        SetState(ConnectionEvent.ConnectRequested);
        _runLoopTask = RunConnectionLoopAsync(_lifetimeCts.Token);
        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        _lifetimeCts?.Cancel();
        _trustDecisionSource?.TrySetResult(false);
        SetState(ConnectionEvent.DisconnectRequested);
        CleanupSocket();

        if (_runLoopTask is not null)
        {
            try { await _runLoopTask; } catch { /* a task observa seu próprio cancelamento */ }
        }
    }

    public async Task SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
    {
        if (_sendQueue is null)
            return;

        var json = RemoteMessageSerializer.Serialize(message);
        await _sendQueue.Writer.WriteAsync(json, cancellationToken).ConfigureAwait(false);
    }

    public Task TrustCurrentCertificateAsync()
    {
        if (_connectionInfo is not null && _pendingFingerprint is not null)
            _trustStore.Trust(_connectionInfo.HostAndPort, _pendingFingerprint);

        _trustDecisionSource?.TrySetResult(true);
        return Task.CompletedTask;
    }

    private async Task RunConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Uma exceção de IO disparada pelo próprio DisconnectAsync (socket fechado sob a
                // leitura) não deve sobrescrever o estado Disconnected já emitido por ele.
                if (!cancellationToken.IsCancellationRequested)
                    SetState(ConnectionEvent.TransportFailed);
            }

            CleanupSocket();

            if (!_stateMachine.ShouldAutoReconnect || cancellationToken.IsCancellationRequested)
                return;

            try
            {
                await Task.Delay(ReconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            SetState(ConnectionEvent.ConnectRequested);
        }
    }

    private async Task ConnectOnceAsync(CancellationToken cancellationToken)
    {
        var connectionInfo = _connectionInfo!;
        _pendingFingerprint = null;

        _tcpClient = new TcpClient { NoDelay = true };
        await _tcpClient.ConnectAsync(connectionInfo.Host, connectionInfo.Port, cancellationToken).ConfigureAwait(false);

        _sslStream = new SslStream(_tcpClient.GetStream(), leaveInnerStreamOpen: false, ValidateServerCertificate);

        var authOptions = new SslClientAuthenticationOptions
        {
            TargetHost = connectionInfo.Host,
            EnabledSslProtocols = SslProtocols.Tls12,
        };

        try
        {
            await _sslStream.AuthenticateAsClientAsync(authOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthenticationException) when (_pendingFingerprint is not null)
        {
            // ValidateServerCertificate recusou por falta de confiança (modo inseguro, TOFU):
            // a própria AuthenticateAsClientAsync lança em vez de retornar — pede confirmação
            // ao usuário e, se aceito, refaz a conexão do zero (o SslStream atual é inutilizável).
            CleanupSocket();
            SetState(ConnectionEvent.UntrustedCertificateEncountered);
            CertificateTrustRequested?.Invoke(this, _pendingFingerprint);

            _trustDecisionSource = new TaskCompletionSource<bool>();
            var trusted = await _trustDecisionSource.Task.ConfigureAwait(false);
            _pendingFingerprint = null;

            if (!trusted || cancellationToken.IsCancellationRequested)
            {
                SetState(ConnectionEvent.DisconnectRequested);
                return;
            }

            SetState(ConnectionEvent.CertificateTrusted);
            await ConnectOnceAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        SetState(ConnectionEvent.TlsReady);

        _sendQueue = Channel.CreateUnbounded<string>();
        var sendLoop = RunSendLoopAsync(_sslStream, _sendQueue.Reader, cancellationToken);

        await SendHandshakeAsync(connectionInfo, cancellationToken).ConfigureAwait(false);

        // Um LineFramer novo por tentativa: reaproveitar a mesma instância entre reconexões
        // deixaria bytes parciais de uma conexão derrubada corromperem a primeira mensagem da
        // próxima tentativa.
        var lineFramer = new LineFramer();
        await foreach (var line in lineFramer.ReadLinesAsync(_sslStream, cancellationToken).ConfigureAwait(false))
        {
            HandleLine(line);
        }

        await sendLoop.ConfigureAwait(false);

        // O laço acima só termina quando o relay fecha o socket (EOF) sem lançar excessão —
        // se isso aconteceu enquanto já estávamos autenticados, o estado precisa refletir a
        // queda para a UI parar de dizer "Conectado" e a reconexão automática entrar em ação.
        if (State == ConnectionState.Authenticated)
            SetState(ConnectionEvent.TransportFailed);
    }

    private async Task SendHandshakeAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken)
    {
        await SendAsync(new ProtocolVersionMessage(), cancellationToken).ConfigureAwait(false);
        await SendAsync(new JoinMessage(connectionInfo.Channel, RemoteConnectionMode.Master), cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunSendLoopAsync(Stream stream, ChannelReader<string> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var json in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(LineFramer.FrameLine(json));
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
    }

    private void HandleLine(string line)
    {
        if (!RemoteMessageParser.TryParse(line, out var message))
            return;

        switch (message)
        {
            case PingMessage:
                return; // nunca respondido, ver relatório §2/§10

            case ChannelJoinedMessage:
                SetState(ConnectionEvent.ChannelJoined);
                break;

            case ErrorMessage error:
                // O relatório do protocolo só documenta "incorrect_password" durante o
                // handshake, mas qualquer "error" recebido antes de channel_joined indica que
                // o relay vai fechar o socket — tratamos genericamente como falha de auth em
                // vez de deixar a conexão "pendurada" em Handshaking. Um error recebido DEPOIS
                // de autenticado não mexe no estado da conexão, só é repassado como aviso.
                if (State != ConnectionState.Authenticated)
                {
                    SetState(ConnectionEvent.IncorrectPassword);
                    return;
                }
                break;

            case VersionMismatchMessage:
                SetState(ConnectionEvent.VersionMismatch);
                return;
        }

        MessageReceived?.Invoke(this, message);
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        if (_connectionInfo is null || !_connectionInfo.Insecure || certificate is null)
            return false;

        var fingerprint = ComputeFingerprint(certificate);

        if (_trustStore.IsTrusted(_connectionInfo.HostAndPort, fingerprint))
            return true;

        // Marca como pendente; ConnectOnceAsync detecta _pendingFingerprint após o handshake TLS
        // falhar aqui (retorna false) e reabre a conexão só depois de confirmação do usuário.
        _pendingFingerprint = fingerprint;
        return false;
    }

    private static string ComputeFingerprint(X509Certificate certificate)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(certificate.GetRawCertData());
        return Convert.ToHexStringLower(hash);
    }

    private void SetState(ConnectionEvent connectionEvent)
    {
        var newState = _stateMachine.Apply(connectionEvent);
        StateChanged?.Invoke(this, newState);
    }

    private void CleanupSocket()
    {
        _sendQueue?.Writer.TryComplete();
        _sslStream?.Dispose();
        _tcpClient?.Dispose();
        _sslStream = null;
        _tcpClient = null;
        _sendQueue = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _lifetimeCts?.Dispose();
    }
}
