using System.Text.Json;
using NvdaRemoteDroid.Core.Protocol.Messages;

namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>
/// Desserializa uma linha JSON recebida do relay/follower. Nunca lança: tipo desconhecido ou
/// mensagem com campo obrigatório ausente resultam em UnknownMessage (ou são descartados),
/// nunca em exceção propagada — campos extras são sempre tolerados, exatamente como o NVDA
/// tolera no lado dele (ver relatório §6).
/// </summary>
public static class RemoteMessageParser
{
    public static bool TryParse(string json, out IRemoteMessage message)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                message = new UnknownMessage(Type: "(sem type)", RawJson: json);
                return true;
            }

            var type = typeElement.GetString()!;

            message = type switch
            {
                RemoteMessageType.ChannelJoined => Deserialize<ChannelJoinedMessage>(root, json, m => m.UserIds is not null),
                RemoteMessageType.ClientJoined => Deserialize<ClientJoinedMessage>(root, json),
                RemoteMessageType.ClientLeft => Deserialize<ClientLeftMessage>(root, json),
                RemoteMessageType.Speak => Deserialize<SpeakMessage>(root, json, m => m.Sequence is not null),
                RemoteMessageType.Cancel => new CancelMessage(),
                RemoteMessageType.PauseSpeech => Deserialize<PauseSpeechMessage>(root, json),
                RemoteMessageType.Tone => Deserialize<ToneMessage>(root, json),
                RemoteMessageType.Wave => Deserialize<WaveMessage>(root, json, m => m.FileName is not null),
                RemoteMessageType.SetClipboardText => Deserialize<SetClipboardTextMessage>(root, json, m => m.Text is not null),
                RemoteMessageType.Motd => Deserialize<MotdMessage>(root, json, m => m.Motd is not null),
                RemoteMessageType.VersionMismatch => new VersionMismatchMessage(),
                RemoteMessageType.Ping => new PingMessage(),
                RemoteMessageType.Error => Deserialize<ErrorMessage>(root, json, m => m.Message is not null),
                _ => new UnknownMessage(type, json),
            };

            return true;
        }
        catch (JsonException)
        {
            message = new UnknownMessage(Type: "(json inválido)", RawJson: json);
            return false;
        }
    }

    private static IRemoteMessage Deserialize<T>(JsonElement root, string rawJson, Func<T, bool>? isValid = null) where T : IRemoteMessage
    {
        try
        {
            var result = root.Deserialize<T>();
            if (result is not null && (isValid is null || isValid(result)))
                return result;
        }
        catch (JsonException)
        {
            // Campo obrigatório ausente/tipo incompatível: cai para UnknownMessage abaixo.
        }

        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "?" : "?";
        return new UnknownMessage(type, rawJson);
    }
}
