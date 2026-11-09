using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;

namespace NvdaRemoteDroid.ViewModels;

/// <summary>Implementação Android da tela de Controle Remoto.</summary>
public sealed class RemoteControlViewModel : RemoteControlViewModelBase
{
    public RemoteControlViewModel(
        ITransportService transportService,
        ISpeechOutputService speechOutputService,
        IClipboardService clipboardService,
        IMotdSeenStore motdSeenStore)
        : base(transportService, speechOutputService, clipboardService, motdSeenStore)
    {
    }

    protected override void AnnounceStatus(string text) =>
        MainThread.BeginInvokeOnMainThread(() => SemanticScreenReader.Default.Announce(text));

    protected override void ShowMotd(string motdText) =>
        MainThread.BeginInvokeOnMainThread(() =>
            _ = Shell.Current.DisplayAlertAsync(Core.Resources.Strings.AppResources.RemoteControl_Title, motdText, "OK"));

    protected override void PlayTone(double hz, int lengthMs)
    {
        // Best-effort: sem API de beep simples e multiplataforma no MAUI: usamos o próprio
        // tom como feedback por vibração curta, suficiente para v1 (ver escopo no plano).
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
