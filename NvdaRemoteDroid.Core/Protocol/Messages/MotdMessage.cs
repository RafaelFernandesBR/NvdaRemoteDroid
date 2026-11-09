using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Mensagem do dia enviada pelo relay; o app deve mostrá-la uma vez por texto (dedupe local).</summary>
public sealed record MotdMessage(
    [property: JsonPropertyName("motd")] string Motd,
    [property: JsonPropertyName("force_display")] bool? ForceDisplay = null
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Motd;
}
