using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Solicita Ctrl+Alt+Del no follower (requer UI Access + política SoftwareSASGeneration lá).</summary>
public sealed record SendSasMessage : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.SendSas;
}
