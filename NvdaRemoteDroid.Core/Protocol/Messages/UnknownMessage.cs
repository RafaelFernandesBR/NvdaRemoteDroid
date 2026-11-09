namespace NvdaRemoteDroid.Core.Protocol.Messages;

/// <summary>
/// Qualquer "type" não mapeado (ex. index, nvda_not_connected, generate_key não implementado
/// no relay self-hosted) ou mensagem com campo obrigatório ausente. Descartada com segurança,
/// nunca propaga exceção para quem chama o parser.
/// </summary>
public sealed record UnknownMessage(string Type, string RawJson) : IRemoteMessage;
