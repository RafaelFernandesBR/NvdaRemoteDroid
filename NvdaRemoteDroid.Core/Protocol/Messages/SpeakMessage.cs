using System.Text.Json.Serialization;
using NvdaRemoteDroid.Core.Protocol.SpeechSequence;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Texto (e comandos de fala) que o follower quer que o master fale em voz alta.</summary>
public sealed record SpeakMessage(
    [property: JsonPropertyName("sequence"), JsonConverter(typeof(SpeechSequenceJsonConverter))]
    List<ISpeechSequenceItem> Sequence,
    [property: JsonPropertyName("priority")] int Priority = 0
) : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Speak;
}
