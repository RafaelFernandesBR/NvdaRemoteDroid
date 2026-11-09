namespace NvdaRemoteDroid.Core.Gateway;

/// <summary>Deduplica mensagens "motd" já exibidas ao usuário (por hash de texto), persistido entre sessões.</summary>
public interface IMotdSeenStore
{
    bool HasSeen(string motdText);
    void MarkSeen(string motdText);
}
