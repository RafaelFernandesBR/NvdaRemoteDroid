namespace NvdaRemoteDroid.Core.Transport;

/// <summary>Porta padrão do protocolo NVDA Remote (relay self-hosted e relay público).</summary>
public static class RemoteDefaults
{
    public const int Port = 6837;
    public const string PublicRelayHost = "nvdaremote.com";
}

/// <summary>Parâmetros de uma tentativa de conexão com um relay NVDA Remote.</summary>
public sealed record ConnectionInfo(
    string Host,
    int Port,
    string Channel,
    bool Insecure
)
{
    public static ConnectionInfo PublicRelay(string channel, bool insecure = false) =>
        new(RemoteDefaults.PublicRelayHost, RemoteDefaults.Port, channel, insecure);

    public string HostAndPort => $"{Host}:{Port}";
}
