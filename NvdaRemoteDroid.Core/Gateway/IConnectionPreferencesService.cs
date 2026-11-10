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

    /// <summary>Último grupo de teclas aberto no teclado virtual, reaberto na próxima vez.</summary>
    Keyboard.VirtualKeyboardGroupId KeyboardGroup { get; set; }
}

public enum VirtualKeyNvdaChoice
{
    Insert,
    CapsLock,
}
