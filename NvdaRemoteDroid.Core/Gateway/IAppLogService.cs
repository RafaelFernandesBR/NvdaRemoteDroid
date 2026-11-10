namespace NvdaRemoteDroid.Core.Gateway;

/// <summary>Acesso ao arquivo de log técnico da sessão atual, para a tela de Configurações
/// oferecer "Compartilhar logs". A escrita de fato acontece via ILoggerProvider (ver
/// implementação em NvdaRemoteDroid/Services/AppLogService.cs) — esta interface só expõe o que
/// a camada de UI (portável, sem MAUI) precisa para decidir se há algo para compartilhar.</summary>
public interface IAppLogService
{
    string LogFilePath { get; }

    bool HasLogs { get; }
}
