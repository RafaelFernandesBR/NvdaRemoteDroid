using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada pelo relay quando a versão de protocolo é incompatível; falha terminal, a conexão deve fechar.</summary>
public sealed record VersionMismatchMessage : IRemoteMessage
{
    [JsonPropertyName("type")]
    public string Type => RemoteMessageType.VersionMismatch;
}
