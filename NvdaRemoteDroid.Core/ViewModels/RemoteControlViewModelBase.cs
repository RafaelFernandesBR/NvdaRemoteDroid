using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Session;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Core.ViewModels;

/// <summary>
/// Lógica da tela de Controle Remoto: envia teclas/SAS/clipboard/texto e reflete o estado da
/// conexão. O que chega do NVDA (fala, tons, clipboard, motd) NÃO passa por aqui — quem trata é
/// o RemoteSessionCoordinator (singleton), para funcionar com esta tela fechada.
/// </summary>
public abstract partial class RemoteControlViewModelBase : ObservableObject
{
    private readonly ITransportService _transportService;
    private readonly IClipboardService _clipboardService;
    private readonly IRemoteSessionCoordinator _sessionCoordinator;

    private ConnectionState _connectionState;

    protected RemoteControlViewModelBase(
        ITransportService transportService,
        IClipboardService clipboardService,
        IRemoteSessionCoordinator sessionCoordinator)
    {
        _transportService = transportService;
        _clipboardService = clipboardService;
        _sessionCoordinator = sessionCoordinator;

        _transportService.StateChanged += OnTransportStateChanged;
        _sessionCoordinator.PresenceChanged += OnPresenceChanged;

        // Se a autenticação já aconteceu antes desta instância existir (ex.: VM transient
        // criado pela navegação do Shell só depois do evento StateChanged.Authenticated já ter
        // disparado e sido perdido), inicializa a partir do estado atual em vez de esperar a
        // próxima transição.
        ApplyState(_transportService.State);
    }

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendSasCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendClipboardCommand))]
    [NotifyCanExecuteChangedFor(nameof(SendTextCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool isConnected;

    /// <summary>Texto digitado no campo "Digitar texto", enviado por SendTextCommand.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendTextCommand))]
    private string textToSend = string.Empty;

    private bool CanSendText => IsConnected && !string.IsNullOrEmpty(TextToSend);

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

    /// <summary>
    /// Envia uma tecla "embrulhada" por modificadores: todos descem na ordem dada, a tecla é
    /// tocada, e todos sobem na ordem inversa — todo down tem seu up (relatório §5).
    /// </summary>
    public async Task SendChordAsync(IReadOnlyList<VirtualKeyMapping> modifiers, VirtualKeyMapping key)
    {
        foreach (var modifier in modifiers)
            await SendKeyDownAsync(modifier);

        await SendKeyAsync(key);

        for (var i = modifiers.Count - 1; i >= 0; i--)
            await SendKeyUpAsync(modifiers[i]);
    }

    [RelayCommand]
    private async Task ReleaseAllModifiersAsync()
    {
        var (down, up) = KeyEventFactory.CreateReleaseAllPair();
        await _transportService.SendAsync(down);
        await _transportService.SendAsync(up);
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private Task SendSasAsync() => _transportService.SendAsync(new SendSasMessage());

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task SendClipboardAsync()
    {
        var text = await _clipboardService.GetTextAsync();
        if (!string.IsNullOrEmpty(text))
            await _transportService.SendAsync(new SetClipboardTextMessage(text));
    }

    /// <summary>
    /// "Digita" um texto no computador remoto colando-o: set_clipboard_text + Ctrl+V. Mandar
    /// tecla por tecla dependeria do layout de teclado do PC (ABNT2 x US) e não cobriria
    /// acentos; colar funciona com qualquer texto, ao custo de substituir o clipboard remoto.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSendText))]
    private async Task SendTextAsync()
    {
        // SendAsync descarta em silêncio fora de Authenticated: se a conexão caiu entre o toque
        // e aqui, o texto fica no campo em vez de ser "enviado" para lugar nenhum.
        var text = TextToSend;
        if (string.IsNullOrEmpty(text) || _transportService.State != ConnectionState.Authenticated)
            return;

        await _transportService.SendAsync(new SetClipboardTextMessage(text));
        await SendChordAsync([VirtualKeyboardLayout.Ctrl], new VirtualKeyMapping(VirtualKeyCode.V, false));

        TextToSend = string.Empty;
        AnnounceStatus(AppResources.RemoteControl_TextSent);
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private Task DisconnectAsync() => _transportService.DisconnectAsync();

    [RelayCommand]
    private void GoToConnection() => NavigateToConnection();

    private void OnTransportStateChanged(object? sender, ConnectionState state) => ApplyState(state);

    private void OnPresenceChanged(object? sender, EventArgs e) => ApplyState(_connectionState);

    private void ApplyState(ConnectionState state)
    {
        _connectionState = state;
        IsConnected = state == ConnectionState.Authenticated;
        StatusText = IsConnected && !_sessionCoordinator.IsRemoteComputerPresent
            ? AppResources.Status_AuthenticatedWaitingRemote
            : ConnectionStateDescriber.Describe(state);
    }

    /// <summary>Anuncia texto via leitor de tela (SemanticScreenReader no Android).</summary>
    protected abstract void AnnounceStatus(string text);

    /// <summary>Leva o usuário à aba de Conexão (usado quando esta tela é aberta sem conexão).</summary>
    protected abstract void NavigateToConnection();
}
