using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada aos demais peers quando alguém sai do canal.</summary>
public sealed record ClientLeftMessage(
    [property: JsonPropertyName("user_id")] int UserId,
    [property: JsonPropertyName("client")] ClientInfo? Client = null
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.ClientLeft;
}
