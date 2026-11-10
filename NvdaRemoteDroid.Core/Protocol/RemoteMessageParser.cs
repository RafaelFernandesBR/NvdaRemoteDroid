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

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                message = new UnknownMessage(Type: "(sem type)", RawJson: json);
                return true;
            }

            var type = typeElement.GetString()!;

            message = type switch
            {
                // Mensagens de canal são lidas à mão, campo a campo: o "type" sozinho já basta
                // para a transição de estado, e um campo em formato inesperado (ou ausente) no
                // channel_joined não pode deixar a conexão presa em Handshaking.
                RemoteMessageType.ChannelJoined => ParseChannelJoined(root),
                RemoteMessageType.ClientJoined => new ClientJoinedMessage(ReadId(root, "user_id"), ReadClient(root, "client")),
                RemoteMessageType.ClientLeft => new ClientLeftMessage(ReadId(root, "user_id"), ReadClient(root, "client")),
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

    private static ChannelJoinedMessage ParseChannelJoined(JsonElement root)
    {
        var channel = root.TryGetProperty("channel", out var channelElement) && channelElement.ValueKind == JsonValueKind.String
            ? channelElement.GetString() ?? string.Empty
            : string.Empty;

        var userIds = new List<long>();
        if (root.TryGetProperty("user_ids", out var idsElement) && idsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in idsElement.EnumerateArray())
            {
                if (TryReadId(item, out var id))
                    userIds.Add(id);
            }
        }

        var clients = new List<ClientInfo>();
        if (root.TryGetProperty("clients", out var clientsElement) && clientsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in clientsElement.EnumerateArray())
            {
                if (TryReadClient(item, out var client))
                    clients.Add(client);
            }
        }

        return new ChannelJoinedMessage(channel, userIds, clients);
    }

    private static long? ReadId(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && TryReadId(element, out var id) ? id : null;

    private static ClientInfo? ReadClient(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && TryReadClient(element, out var client) ? client : null;

    /// <summary>Aceita id como inteiro, número com casas decimais ou string numérica.</summary>
    private static bool TryReadId(JsonElement element, out long id)
    {
        id = 0;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt64(out id))
                    return true;

                if (element.TryGetDouble(out var asDouble) && asDouble is >= long.MinValue and <= long.MaxValue)
                {
                    id = (long)asDouble;
                    return true;
                }

                return false;

            case JsonValueKind.String:
                return long.TryParse(element.GetString(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out id);

            default:
                return false;
        }
    }

    private static bool TryReadClient(JsonElement element, out ClientInfo client)
    {
        client = null!;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("id", out var idElement) || !TryReadId(idElement, out var id))
            return false;

        var connectionType = element.TryGetProperty("connection_type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;

        client = new ClientInfo(id, connectionType);
        return true;
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
