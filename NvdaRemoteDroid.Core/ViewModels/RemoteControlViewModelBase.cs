using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>
/// Lógica da tela de Controle Remoto: envia teclas/SAS/clipboard, recebe e reproduz speak,
/// trata cancel/pause_speech/motd/tone. Teclas em si (teclado virtual na tela) são compostas
/// pela View usando KeyEventFactory + AndroidKeyToVirtualKeyMap; esta classe só expõe o envio.
/// </summary>
public abstract partial class RemoteControlViewModelBase : ObservableObject
{
    private readonly ITransportService _transportService;
    private readonly ISpeechOutputService _speechOutputService;
    private readonly IClipboardService _clipboardService;
    private readonly IMotdSeenStore _motdSeenStore;

    protected RemoteControlViewModelBase(
        ITransportService transportService,
        ISpeechOutputService speechOutputService,
        IClipboardService clipboardService,
        IMotdSeenStore motdSeenStore)
    {
        _transportService = transportService;
        _speechOutputService = speechOutputService;
        _clipboardService = clipboardService;
        _motdSeenStore = motdSeenStore;

        _transportService.MessageReceived += OnMessageReceived;
        _transportService.StateChanged += OnTransportStateChanged;

        // Se a autenticação já aconteceu antes desta instância existir (ex.: VM transient
        // criado pela navegação do Shell só depois do evento StateChanged.Authenticated já ter
        // disparado e sido perdido), inicializa a partir do estado atual em vez de esperar a
        // próxima transição — sem invocar o handler, que faz mais do que isso.
        var currentState = _transportService.State;
        IsConnected = currentState == ConnectionState.Authenticated;
        StatusText = ConnectionStateDescriber.Describe(currentState);
    }

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private bool isConnected;

    public async Task SendKeyAsync(VirtualKeyMapping mapping)
    {
        var (down, up) = KeyEventFactory.CreateTapPair(mapping);
        await _transportService.SendAsync(down);
        await _transportService.SendAsync(up);
    }

    public async Task SendKeyDownAsync(VirtualKeyMapping mapping) =>
        await _transportService.SendAsync(KeyEventFactory.Create(mapping, pressed: true));

    public async Task SendKeyUpAsync(VirtualKeyMapping mapping) =>
        await _transportService.SendAsync(KeyEventFactory.Create(mapping, pressed: false));

    [RelayCommand]
    private async Task ReleaseAllModifiersAsync()
    {
        var (down, up) = KeyEventFactory.CreateReleaseAllPair();
        await _transportService.SendAsync(down);
        await _transportService.SendAsync(up);
    }

    [RelayCommand]
    private Task SendSasAsync() => _transportService.SendAsync(new SendSasMessage());

    [RelayCommand]
    private async Task SendClipboardAsync()
    {
        var text = await _clipboardService.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
            await _transportService.SendAsync(new SetClipboardTextMessage(text));
    }

    [RelayCommand]
    private Task DisconnectAsync() => _transportService.DisconnectAsync();

    private void OnTransportStateChanged(object? sender, ConnectionState state)
    {
        IsConnected = state == ConnectionState.Authenticated;
        StatusText = ConnectionStateDescriber.Describe(state);
    }

    private void OnMessageReceived(object? sender, IRemoteMessage message)
    {
        switch (message)
        {
            case SpeakMessage speak:
                HandleSpeak(speak);
                break;

            case CancelMessage:
                _speechOutputService.Cancel();
                break;

            case PauseSpeechMessage pause:
                _speechOutputService.SetPaused(pause.Switch);
                break;

            case SetClipboardTextMessage clipboard:
                _ = _clipboardService.SetTextAsync(clipboard.Text);
                break;

            case MotdMessage motd:
                HandleMotd(motd);
                break;

            case ToneMessage tone:
                PlayTone(tone.Hz, tone.Length);
                break;

            case ErrorMessage error:
                AnnounceStatus($"Erro do relay: {error.Message}");
                break;
        }
    }

    private void HandleSpeak(SpeakMessage speak)
    {
        var priority = (SpeechPriority)Math.Clamp(speak.Priority, 0, 2);
        foreach (var utterance in SpeechSequenceTextBuilder.ToUtterances(speak.Sequence))
        {
            _speechOutputService.Speak(utterance, priority);
        }
    }

    private void HandleMotd(MotdMessage motd)
    {
        if (_motdSeenStore.HasSeen(motd.Motd) && motd.ForceDisplay != true)
            return;

        _motdSeenStore.MarkSeen(motd.Motd);
        ShowMotd(motd.Motd);
    }

    /// <summary>Anuncia texto via leitor de tela (SemanticScreenReader no Android).</summary>
    protected abstract void AnnounceStatus(string text);

    /// <summary>Exibe a mensagem do dia (motd) ao usuário.</summary>
    protected abstract void ShowMotd(string motdText);

    /// <summary>Reproduz um bip best-effort (tone.hz/tone.length).</summary>
    protected abstract void PlayTone(double hz, int lengthMs);
}
