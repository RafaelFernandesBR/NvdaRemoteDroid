using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Bidirecional: sincroniza texto da área de transferência entre os dois lados.</summary>
public sealed record SetClipboardTextMessage(
    [property: JsonPropertyName("text")] string Text
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.SetClipboardText;
}
