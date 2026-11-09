using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Cancela a fala TTS local em andamento.</summary>
public sealed record CancelMessage : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Cancel;
}
