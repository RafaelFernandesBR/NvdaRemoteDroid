using NvdaRemoteDroid.Core.Session;
using AppRes = NvdaRemoteDroid.Core.Resources.Strings.AppResources;

namespace NvdaRemoteDroid.Services;

/// <summary>
/// Retorno ao usuário para eventos da sessão remota (ISessionFeedback): anúncio pelo leitor de
/// tela, diálogo da mensagem do dia e vibração no lugar do bip do NVDA.
/// </summary>
public sealed class SessionFeedbackService : ISessionFeedback
{
    public void Announce(string text) =>
        MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Default.Announce(text));

    public void ShowMotd(string motdText) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Shell.Current é null com a Activity destruída (sessão viva só pelo serviço em
            // primeiro plano); nesse caso a mensagem do dia é simplesmente pulada.
            if (Shell.Current is { } shell)
                _ = shell.DisplayAlertAsync(AppRes.RemoteControl_Title, motdText, AppRes.Common_Ok);
        });

    public void PlayTone(double hz, int lengthMs)
    {
        // Best-effort: sem API de beep simples e multiplataforma no MAUI, o tom vira uma
        // vibração curta — suficiente como sinal de "o NVDA bipou".
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(Math.Clamp(lengthMs, 20, 300)));
        }
        catch
        {
            // Vibração pode não estar disponível no device; falha silenciosa é aceitável aqui.
        }
    }
}
