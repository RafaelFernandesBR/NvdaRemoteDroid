using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>switch=true pausa a fala TTS local; switch=false retoma.</summary>
public sealed record PauseSpeechMessage(
    [property: JsonPropertyName("switch")] bool Switch
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.PauseSpeech;
}
