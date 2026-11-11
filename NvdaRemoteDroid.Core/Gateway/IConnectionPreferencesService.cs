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

    /// <summary>Último modo da área de gestos (Objeto, Texto ou Setas).</summary>
    Gestures.GesturePadMode GesturePadMode { get; set; }

    /// <summary>Pacote do motor de TTS escolhido (vazio = padrão do dispositivo).</summary>
    string TtsEnginePackage { get; set; }

    /// <summary>Velocidade da fala (0.5 a 2.0; 1.0 = normal).</summary>
    float SpeechRate { get; set; }
}

public enum VirtualKeyNvdaChoice
{
    Insert,
    CapsLock,
}
