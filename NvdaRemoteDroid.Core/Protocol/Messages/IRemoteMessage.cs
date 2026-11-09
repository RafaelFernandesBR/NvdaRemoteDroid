namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>Marcador comum a toda mensagem do protocolo NVDA Remote, de e para o fio.</summary>
public interface IRemoteMessage
{
    string Type { get; }
}
