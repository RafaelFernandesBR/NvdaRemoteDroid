namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Descreve um peer do canal, presente em channel_joined/client_joined/client_left (protocolVersion >= 2).
/// ConnectionType vem vazio quando o relay não informa — nunca null.
/// </summary>
public sealed record ClientInfo(long Id, string ConnectionType)
{
    /// <summary>True quando o peer é a máquina controlada ("slave" no fio — ver relatório §3).</summary>
    public bool IsFollower => string.Equals(ConnectionType, "slave", StringComparison.OrdinalIgnoreCase);
}
