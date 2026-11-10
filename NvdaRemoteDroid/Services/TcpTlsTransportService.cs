using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
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
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(15);

    private readonly ICertificateTrustStore _trustStore;
    private readonly ILogger<TcpTlsTransportService> _logger;
    private readonly RemoteConnectionStateMachine _stateMachine = new();
    private readonly object _stateGate = new();

    // Serializa Connect/Disconnect: os dois podem vir de lugares diferentes ao mesmo tempo
    // (botão da tela, ação "Desconectar" da notificação, prompt de certificado recusado).
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private Channel<string>? _sendQueue;
    private CancellationTokenSource? _lifetimeCts;
    private Task? _runLoopTask;

    private ConnectionInfo? _connectionInfo;
    private string? _pendingFingerprint;

    // Pedido de confiança em aberto: host, fingerprint e decisão andam juntos, para um "Confiar"
    // tardio nunca gravar o fingerprint de outra tentativa de conexão.
    private sealed record TrustRequest(string HostAndPort, string Fingerprint, TaskCompletionSource<bool> Decision);

    private TrustRequest? _trustRequest;

    public TcpTlsTransportService(ICertificateTrustStore trustStore, ILogger<TcpTlsTransportService> logger)
    {
        _trustStore = trustStore;
        _logger = logger;
    }

    public ConnectionState State
    {
        get
        {
            lock (_stateGate)
                return _stateMachine.State;
        }
    }

    public event EventHandler<ConnectionState>? StateChanged;
    public event EventHandler<IRemoteMessage>? MessageReceived;
    public event EventHandler<string>? CertificateTrustRequested;

    public async Task ConnectAsync(ConnectionInfo connectionInfo, CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Nunca deixa dois laços de conexão vivos: um ConnectAsync com outro ainda rodando
            // primeiro encerra o anterior.
            await StopLoopAsync().ConfigureAwait(false);

            _connectionInfo = connectionInfo;
            var lifetimeCts = new CancellationTokenSource();
            _lifetimeCts = lifetimeCts;
            SetState(ConnectionEvent.ConnectRequested);
            _runLoopTask = Task.Run(() => RunConnectionLoopAsync(lifetimeCts.Token), CancellationToken.None);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _lifetimeCts?.Cancel();
            SetState(ConnectionEvent.DisconnectRequested);
            await StopLoopAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopLoopAsync()
    {
        var lifetimeCts = _lifetimeCts;
        var runLoopTask = _runLoopTask;
        _lifetimeCts = null;
        _runLoopTask = null;

        lifetimeCts?.Cancel();
        _trustRequest?.Decision.TrySetResult(false);
        CleanupSocket();

        if (runLoopTask is not null)
        {
            try { await runLoopTask.ConfigureAwait(false); } catch { /* a task observa seu próprio cancelamento */ }
        }

        // De novo, depois de o laço terminar: ele pode ter criado um socket entre o primeiro
        // CleanupSocket e a percepção do cancelamento.
        CleanupSocket();

        lifetimeCts?.Dispose();
    }

    /// <summary>
    /// Enfileira uma mensagem de sessão (tecla, SAS, clipboard). Só tem efeito com o canal já
    /// autenticado: antes disso o relay ainda não repassa nada ao follower, e teclas tocadas
    /// durante uma reconexão não devem "chegar atrasadas" depois. Nunca lança por causa do
    /// estado da conexão — quem chama são handlers de UI (async void) e o DispatchKeyEvent.
    /// </summary>
    public Task SendAsync(IRemoteMessage message, CancellationToken cancellationToken = default)
    {
        if (State == ConnectionState.Authenticated)
            Enqueue(_sendQueue, message);

        return Task.CompletedTask;
    }

    private static void Enqueue(Channel<string>? queue, IRemoteMessage message)
    {
        // TryWrite em vez de WriteAsync: a fila é ilimitada (nunca bloqueia) e, se o
        // CleanupSocket de outra thread já a completou, a mensagem é só descartada.
        queue?.Writer.TryWrite(RemoteMessageSerializer.Serialize(message));
    }

    public Task TrustCurrentCertificateAsync()
    {
        // Só grava a confiança se este pedido ainda estava em aberto (TrySetResult vence a
        // corrida com um cancelamento), e sempre com o host/fingerprint do próprio pedido.
        var request = _trustRequest;
        if (request is not null && request.Decision.TrySetResult(true))
            _trustStore.Trust(request.HostAndPort, request.Fingerprint);

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
                CleanupSocket();
                return;
            }
            catch (Exception ex)
            {
                // Uma exceção de IO disparada pelo próprio DisconnectAsync (socket fechado sob a
                // leitura) não deve sobrescrever o estado Disconnected já emitido por ele.
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Falha de transporte, tentando reconectar em {Delay}s", ReconnectDelay.TotalSeconds);
                    SetState(ConnectionEvent.TransportFailed);
                }
            }

            CleanupSocket();

            if (cancellationToken.IsCancellationRequested || !ShouldAutoReconnect())
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

    private bool ShouldAutoReconnect()
    {
        lock (_stateGate)
            return _stateMachine.ShouldAutoReconnect;
    }

    private async Task ConnectOnceAsync(CancellationToken cancellationToken)
    {
        var connectionInfo = _connectionInfo!;
        _pendingFingerprint = null;

        var tcpClient = new TcpClient { NoDelay = true };
        _tcpClient = tcpClient;
        await tcpClient.ConnectAsync(connectionInfo.Host, connectionInfo.Port, cancellationToken).ConfigureAwait(false);
        EnableKeepAlive(tcpClient.Client);

        var sslStream = new SslStream(tcpClient.GetStream(), leaveInnerStreamOpen: false, ValidateServerCertificate);
        _sslStream = sslStream;

        var authOptions = new SslClientAuthenticationOptions
        {
            TargetHost = connectionInfo.Host,
            EnabledSslProtocols = SslProtocols.Tls12,
        };

        try
        {
            await sslStream.AuthenticateAsClientAsync(authOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthenticationException) when (_pendingFingerprint is not null)
        {
            // ValidateServerCertificate recusou por falta de confiança (modo inseguro, TOFU):
            // a própria AuthenticateAsClientAsync lança em vez de retornar — pede confirmação
            // ao usuário e, se aceito, refaz a conexão do zero (o SslStream atual é inutilizável).
            var fingerprint = _pendingFingerprint!;
            CleanupSocket();
            _logger.LogWarning("Certificado não confiável para {Host}, aguardando confirmação do usuário", connectionInfo.Host);
            SetState(ConnectionEvent.UntrustedCertificateEncountered);

            var trustDecisionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _trustRequest = new TrustRequest(connectionInfo.HostAndPort, fingerprint, trustDecisionSource);
            MainThread.BeginInvokeOnMainThread(() => CertificateTrustRequested?.Invoke(this, fingerprint));

            bool trusted;
            using (cancellationToken.Register(() => trustDecisionSource.TrySetResult(false)))
                trusted = await trustDecisionSource.Task.ConfigureAwait(false);

            _trustRequest = null;
            cancellationToken.ThrowIfCancellationRequested();

            if (!trusted)
            {
                SetState(ConnectionEvent.DisconnectRequested);
                return;
            }

            SetState(ConnectionEvent.CertificateTrusted);
            await ConnectOnceAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        SetState(ConnectionEvent.TlsReady);

        var sendQueue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        _sendQueue = sendQueue;
        var sendLoop = RunSendLoopAsync(sslStream, sendQueue.Reader, cancellationToken);

        // Handshake: protocol_version -> join, direto na fila (SendAsync só aceita mensagens
        // depois de Authenticated).
        Enqueue(sendQueue, new ProtocolVersionMessage());
        Enqueue(sendQueue, new JoinMessage(connectionInfo.Channel, RemoteConnectionMode.Master));

        using var handshakeWatchdogCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = WatchHandshakeAsync(tcpClient, handshakeWatchdogCts.Token);
        var stoppedOnTerminalState = false;

        try
        {
            // Um LineFramer novo por tentativa: reaproveitar a mesma instância entre reconexões
            // deixaria bytes parciais de uma conexão derrubada corromperem a primeira mensagem da
            // próxima tentativa.
            var lineFramer = new LineFramer();
            await foreach (var line in lineFramer.ReadLinesAsync(sslStream, cancellationToken).ConfigureAwait(false))
            {
                // false = estado terminal (AuthFailed/VersionMismatch): para de ler aqui em vez
                // de depender de o relay fechar o socket — senão a conexão ficaria aberta e
                // invisível com a UI já dizendo que falhou.
                if (!HandleLine(line))
                {
                    stoppedOnTerminalState = true;
                    break;
                }
            }
        }
        finally
        {
            handshakeWatchdogCts.Cancel();

            // Completa a fila ANTES de aguardar o laço de envio: ele só termina quando o reader
            // é completado, e CleanupSocket (que também completa) só roda depois deste método.
            sendQueue.Writer.TryComplete();
        }

        await sendLoop.ConfigureAwait(false);

        // O laço de leitura só termina sem exceção quando o relay fecha o socket (EOF) ou no
        // cancelamento. Um EOF é sempre uma queda, em qualquer estado: a state machine mantém
        // AuthFailed/VersionMismatch terminais e leva Handshaking/Authenticated para Error, de
        // onde a reconexão automática entra em ação.
        if (!cancellationToken.IsCancellationRequested && !stoppedOnTerminalState)
        {
            _logger.LogWarning("Relay fechou a conexão (estado {State})", State);
            SetState(ConnectionEvent.TransportFailed);
        }
    }

    /// <summary>
    /// Sem keep-alive, um socket ocioso atrás de NAT/operadora morre calado e só é percebido
    /// na próxima escrita. As opções finas (tempo/intervalo) são best-effort: nem todo kernel
    /// Android as expõe, e a falta delas não impede a conexão.
    /// </summary>
    private static void EnableKeepAlive(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 60);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 15);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 4);
        }
        catch (Exception ex) when (ex is SocketException or PlatformNotSupportedException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Se o relay aceitar o TLS mas nunca responder ao join (nem channel_joined, nem error),
    /// derruba o socket: a leitura pendente falha e o laço de conexão trata como queda comum,
    /// em vez de a UI ficar em "Autenticando..." para sempre.
    /// </summary>
    private async Task WatchHandshakeAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(HandshakeTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (State != ConnectionState.Handshaking)
            return;

        _logger.LogWarning("Relay não respondeu ao join em {Timeout}s, derrubando a conexão", HandshakeTimeout.TotalSeconds);
        tcpClient.Dispose();
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
        catch (ObjectDisposedException) { }
    }

    /// <summary>Trata uma linha recebida. Retorna false quando a conexão entrou num estado
    /// terminal e a leitura deve parar.</summary>
    private bool HandleLine(string line)
    {
        if (!RemoteMessageParser.TryParse(line, out var message))
            return true;

        switch (message)
        {
            case PingMessage:
                return true; // nunca respondido, ver relatório §2/§10

            case UnknownMessage unknown:
                // Só tipo, tamanho e formato (chaves + tipo de cada valor) — nunca valores.
                _logger.LogWarning("Mensagem desconhecida do relay: type={Type}, len={Length}, formato={Shape}",
                    SanitizeForLog(unknown.Type), unknown.RawJson.Length, RemoteMessageShape.Describe(unknown.RawJson));
                return true;

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
                    _logger.LogWarning("Autenticação falhou: relay respondeu error={Error}", SanitizeForLog(error.Message));
                    SetState(ConnectionEvent.IncorrectPassword);
                    return false;
                }
                break;

            case VersionMismatchMessage:
                _logger.LogWarning("Relay recusou a versão do protocolo (version_mismatch)");
                SetState(ConnectionEvent.VersionMismatch);
                return false;
        }

        // Na UI thread, como StateChanged: preserva a ordem entre estado e mensagens e deixa os
        // assinantes (coordinator, ViewModels) mexerem em ObservableProperty sem marshaling.
        MainThread.BeginInvokeOnMainThread(() => MessageReceived?.Invoke(this, message));
        return true;
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

    /// <summary>O relay não é confiável para o que escreve em "type" — corta e remove quebras
    /// de linha antes de persistir em log (arquivo depois compartilhado livremente pelo usuário).</summary>
    private static string SanitizeForLog(string value)
    {
        var singleLine = value.Replace("\r", "").Replace("\n", " ");
        return singleLine.Length > 32 ? singleLine[..32] : singleLine;
    }

    private static string ComputeFingerprint(X509Certificate certificate)
    {
        var hash = SHA256.HashData(certificate.GetRawCertData());
        return Convert.ToHexStringLower(hash);
    }

    private void SetState(ConnectionEvent connectionEvent)
    {
        lock (_stateGate)
        {
            var previousState = _stateMachine.State;
            var newState = _stateMachine.Apply(connectionEvent);
            if (newState == previousState)
                return;

            // Despachado na UI thread porque assinantes (ViewModels) mutam ObservableProperty
            // aqui, e este método é chamado tanto do laço de conexão (thread de rede) quanto da
            // UI. O post fica DENTRO do lock: é não bloqueante, e é o que garante que os eventos
            // entram na fila da UI na mesma ordem das transições (senão um Error calculado antes
            // poderia ser entregue depois de um Disconnected).
            MainThread.BeginInvokeOnMainThread(() => StateChanged?.Invoke(this, newState));
        }
    }

    private void CleanupSocket()
    {
        _sendQueue?.Writer.TryComplete();
        try { _sslStream?.Dispose(); } catch { /* socket já derrubado */ }
        try { _tcpClient?.Dispose(); } catch { /* socket já derrubado */ }
        _sslStream = null;
        _tcpClient = null;
        _sendQueue = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _lifecycleLock.Dispose();
    }
}
