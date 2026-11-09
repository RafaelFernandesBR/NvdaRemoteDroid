using System.Text.Json;
using NvdaRemoteDroid.Core.Protocol.Messages;

namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>Serializa mensagens de saída (master -> relay/follower) para uma linha JSON.</summary>
public static class RemoteMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(IRemoteMessage message) => message switch
    {
        ProtocolVersionMessage m => JsonSerializer.Serialize(m, Options),
        JoinMessage m => JsonSerializer.Serialize(m, Options),
        KeyMessage m => JsonSerializer.Serialize(m, Options),
        SendSasMessage m => JsonSerializer.Serialize(m, Options),
        SetClipboardTextMessage m => JsonSerializer.Serialize(m, Options),
        CancelMessage m => JsonSerializer.Serialize(m, Options),
        PauseSpeechMessage m => JsonSerializer.Serialize(m, Options),
        _ => throw new NotSupportedException($"Tipo de mensagem não suportado para envio: {message.GetType().Name}"),
    };
}
