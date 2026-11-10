namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Enviada aos demais peers quando alguém sai do canal. UserId é null se o relay não informar.</summary>
public sealed record ClientLeftMessage(long? UserId, ClientInfo? Client = null) : IRemoteMessage
{
    public string Type => RemoteMessageType.ClientLeft;
}
