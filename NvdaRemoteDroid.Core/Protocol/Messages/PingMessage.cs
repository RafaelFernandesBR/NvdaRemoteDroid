using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Keep-alive do relay a cada ~300s. Nunca responder (não existe "pong" no protocolo).</summary>
public sealed record PingMessage : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.Ping;
}
