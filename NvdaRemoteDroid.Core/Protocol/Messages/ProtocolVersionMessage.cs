using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Primeira mensagem enviada após o handshake TLS. O relatório recomenda sempre mandar
/// version=2 antes do "join", já que a v1 omite campos como "origin"/"clients"/"client".
/// </summary>
public sealed record ProtocolVersionMessage(
    [property: JsonPropertyName("version")] int Version = 2
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.ProtocolVersion;
}
