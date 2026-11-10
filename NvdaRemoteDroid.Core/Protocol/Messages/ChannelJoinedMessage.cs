namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Resposta de sucesso ao join, enviada só a quem entrou no canal. Só o "type" é garantido:
/// channel/user_ids/clients variam entre relays (o self-hosted só manda clients em
/// protocolVersion >= 2; o relay público não tem código-fonte aberto), então todos os campos
/// são opcionais e nunca null — o parser preenche com vazio o que não vier.
/// </summary>
public sealed record ChannelJoinedMessage(
    string Channel,
    IReadOnlyList<long> UserIds,
    IReadOnlyList<ClientInfo> Clients
) : IRemoteMessage
{
    public string Type => RemoteMessageType.ChannelJoined;
}
