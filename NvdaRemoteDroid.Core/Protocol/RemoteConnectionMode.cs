namespace NvdaRemoteDroid.Core.Protocol;

/// <summary>
/// Valores literais de "connection_type" no fio. O protocolo usa "master"/"slave";
/// internamente o NVDA chama isso de leader/follower, mas o nome que viaja na rede é este.
/// </summary>
public static class RemoteConnectionMode
{
    public const string Master = "master";
    public const string Slave = "slave";
}
