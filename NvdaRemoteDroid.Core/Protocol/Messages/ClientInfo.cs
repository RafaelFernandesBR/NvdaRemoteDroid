using System.Text.Json.Serialization;

namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Descreve um peer do canal, presente em channel_joined/client_joined/client_left (protocolVersion >= 2).</summary>
public sealed record ClientInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("connection_type")] string ConnectionType
);
