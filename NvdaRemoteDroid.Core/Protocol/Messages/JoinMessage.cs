using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada após protocol_version para autenticar no canal com a senha/chave.</summary>
public sealed record JoinMessage(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("connection_type")] string ConnectionType
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Join;
}
