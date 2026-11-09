using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Core.Gateway;

/// <summary>Persiste conexões recentes (host/porta/canal) e a preferência de tecla NVDA do usuário.</summary>
public interface IConnectionPreferencesService
{
    IReadOnlyList<ConnectionInfo> GetRecentConnections();
    void SaveRecentConnection(ConnectionInfo connectionInfo);
    void ClearRecentConnections();

    /// <summary>Tecla usada como "tecla NVDA" no teclado virtual: Insert (padrão) ou CapsLock.</summary>
    VirtualKeyNvdaChoice NvdaKey { get; set; }
}

public enum VirtualKeyNvdaChoice
{
    Insert,
    CapsLock,
}
