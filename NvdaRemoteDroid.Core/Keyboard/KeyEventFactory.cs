using NvdaRemoteDroid.Core.Protocol.Messages;

namespace NvdaRemoteDroid.Core.Keyboard;

/// <summary>
/// Centraliza a criação de KeyMessage para garantir o contrato do protocolo: scan_code
/// sempre null, e todo "down" emitido pelo teclado virtual tem seu "up" correspondente.
/// </summary>
public static class KeyEventFactory
{
    public static KeyMessage Create(VirtualKeyMapping mapping, bool pressed) =>
        new((int)mapping.VkCode, mapping.Extended, pressed, ScanCode: null);

    public static (KeyMessage Down, KeyMessage Up) CreateTapPair(VirtualKeyMapping mapping) => (
        Create(mapping, pressed: true),
        Create(mapping, pressed: false)
    );

    /// <summary>Par VK_NONE usado para soltar todos os modificadores sem disparar nenhuma ação.</summary>
    public static (KeyMessage Down, KeyMessage Up) CreateReleaseAllPair() => KeyMessage.ReleaseAllPair();
}
