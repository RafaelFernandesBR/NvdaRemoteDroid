using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Bip emitido pelo follower; reproduzido best-effort no app.</summary>
public sealed record ToneMessage(
    [property: JsonPropertyName("hz")] double Hz,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("left")] int Left = 50,
    [property: JsonPropertyName("right")] int Right = 50
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Tone;
}
