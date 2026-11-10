namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada aos demais peers quando alguém novo entra no canal. UserId é null se o relay não informar.</summary>
public sealed record ClientJoinedMessage(long? UserId, ClientInfo? Client = null) : IRemoteMessage
{
    public string Type => RemoteMessageType.ClientJoined;
}
