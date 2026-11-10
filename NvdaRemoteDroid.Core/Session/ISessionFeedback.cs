namespace NvdaRemoteDroid.Core.Session;

/// <summary>
/// Retorno ao usuário que depende da plataforma (leitor de tela, diálogo, vibração), usado pelo
/// RemoteSessionCoordinator. Implementado no projeto Android; em teste é um substituto.
/// </summary>
public interface ISessionFeedback
{
    /// <summary>Anuncia texto via leitor de tela (SemanticScreenReader no Android).</summary>
    void Announce(string text);

    /// <summary>Exibe a mensagem do dia (motd) ao usuário.</summary>
    void ShowMotd(string motdText);

    /// <summary>Reproduz um bip best-effort (tone.hz/tone.length).</summary>
    void PlayTone(double hz, int lengthMs);
}
