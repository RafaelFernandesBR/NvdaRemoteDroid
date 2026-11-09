using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Caminho de arquivo .wav no sistema do REMETENTE, não os bytes de áudio — inútil para o
/// app Android fora de um log. Fora de escopo v1: só é desserializada, nunca reproduzida.
/// </summary>
public sealed record WaveMessage(
    [property: JsonPropertyName("fileName")] string FileName
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Wave;
}
