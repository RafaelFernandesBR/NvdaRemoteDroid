using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Resposta de sucesso ao join, enviada só a quem entrou no canal.</summary>
public sealed record ChannelJoinedMessage(
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("user_ids")] IReadOnlyList<int> UserIds,
    [property: JsonPropertyName("clients")] IReadOnlyList<ClientInfo>? Clients = null
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.ChannelJoined;
}
