using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Erro do relay (ex. message="incorrect_password"), seguido de fechamento do socket.
/// O NVDA original não trata este tipo — este app deve, explicitamente.
/// </summary>
public sealed record ErrorMessage(
    [property: JsonPropertyName("message")] string Message
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Error;
}
