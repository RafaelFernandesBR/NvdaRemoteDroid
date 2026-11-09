using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada aos demais peers quando alguém novo entra no canal.</summary>
public sealed record ClientJoinedMessage(
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("client")] ClientInfo? Client = null
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.ClientJoined;
}
