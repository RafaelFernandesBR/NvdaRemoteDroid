using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NvdaRemoteDroid.Core.Gateway;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// Log técnico da sessão atual, para diagnóstico via "Compartilhar logs" em Configurações.
/// Persiste só Warning+ num único arquivo em FileSystem.CacheDirectory, truncado a cada
/// abertura do app (não é histórico entre sessões), com cap de tamanho e supressão de
/// mensagens repetidas em sequência. Nunca logar ConnectionInfo/JoinMessage inteiros nem o
/// RawJson de mensagens desconhecidas — só Type e tamanho (ver TcpTlsTransportService) — e,
/// como defesa adicional caso algum valor escape, a chave do canal é redigida antes de
/// escrever, tanto no formato JSON quanto no ToString() default de record ("Channel = ...").
/// </summary>
public sealed class AppLogService : IAppLogService, ILoggerProvider
{
    private const long MaxFileSizeBytes = 512 * 1024;
    private static readonly TimeSpan RepeatSuppressWindow = TimeSpan.FromSeconds(3);

    private static readonly Regex ChannelJsonFieldPattern =
        new(@"""channel""\s*:\s*""[^""]*""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ChannelRecordFieldPattern =
        new(@"Channel\s*=\s*[^,}\r\n]*", RegexOptions.Compiled);

    private readonly object _lock = new();
    private string? _lastLineKey;
    private DateTime _lastLineAt;

    public string LogFilePath { get; } = Path.Combine(FileSystem.CacheDirectory, "nvdaremotedroid_log.txt");

    public bool HasLogs => File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 0;

    public AppLogService()
    {
        try { File.WriteAllText(LogFilePath, string.Empty); }
        catch { /* Falha ao preparar o arquivo de log nunca deve impedir o app de iniciar. */ }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void WriteLine(string category, string message)
    {
        lock (_lock)
        {
            var lineKey = category + "|" + message;
            var now = DateTime.Now;
            if (lineKey == _lastLineKey && now - _lastLineAt < RepeatSuppressWindow)
                return;

            _lastLineKey = lineKey;
            _lastLineAt = now;

            try
            {
                if (File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length >= MaxFileSizeBytes)
                    return;

                var redacted = ChannelJsonFieldPattern.Replace(message, "\"channel\":\"[REDACTED]\"");
                redacted = ChannelRecordFieldPattern.Replace(redacted, "Channel = [REDACTED]");

                File.AppendAllText(LogFilePath, $"{now:yyyy-MM-dd HH:mm:ss} [{category}] {redacted}{Environment.NewLine}");
            }
            catch { /* Diagnóstico não deve derrubar o fluxo principal do app. */ }
        }
    }

    private sealed class FileLogger(AppLogService owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"[{logLevel}] {formatter(state, exception)}";
            if (exception is not null)
                message += Environment.NewLine + exception;

            owner.WriteLine(category, message);
        }
    }
}
