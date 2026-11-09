using System.Text.Json.Serialization;
using NvdaRemoteDroid.Core.Keyboard;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Tecla pressionada/solta, enviada pelo master. scan_code é sempre null: o follower
/// recalcula via MapVirtualKey e ignora o valor recebido.
/// </summary>
public sealed record KeyMessage(
    [property: JsonPropertyName("vk_code")] int VkCode,
    [property: JsonPropertyName("extended")] bool Extended,
    [property: JsonPropertyName("pressed")] bool Pressed,
    [property: JsonPropertyName("scan_code")] int? ScanCode = null
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Key;

    /// <summary>Par usado para resetar o estado dos modificadores sem disparar nenhuma ação (ex. tecla Windows sozinha).</summary>
    public static (KeyMessage Down, KeyMessage Up) ReleaseAllPair() => (
        new KeyMessage((int)VirtualKeyCode.None, Extended: false, Pressed: true),
        new KeyMessage((int)VirtualKeyCode.None, Extended: false, Pressed: false)
    );
}
