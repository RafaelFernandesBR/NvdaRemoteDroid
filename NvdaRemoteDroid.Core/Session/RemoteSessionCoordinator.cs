using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;

namespace NvdaRemoteDroid.Core.Session;

/// <summary>Estado da sessão remota que as telas observam (presença do computador controlado).</summary>
public interface IRemoteSessionCoordinator
{
    /// <summary>True quando há pelo menos um computador controlado ("slave") no canal.</summary>
    bool IsRemoteComputerPresent { get; }

    event EventHandler? PresenceChanged;
}

/// <summary>
/// Dono da sessão remota enquanto o app vive: recebe speak/cancel/pause_speech/clipboard/tone/
/// motd do transporte e acompanha quem está no canal. É singleton e criado na inicialização do
/// app de propósito — se isso morasse numa ViewModel (transient, criada só quando a aba é
/// aberta), a fala do NVDA só começaria depois de o usuário visitar a tela de Controle e
/// pararia de fazer sentido com o app em segundo plano.
/// </summary>
public sealed class RemoteSessionCoordinator : IRemoteSessionCoordinator
{
    private readonly ISpeechOutputService _speechOutputService;
    private readonly IClipboardService _clipboardService;
    private readonly IMotdSeenStore _motdSeenStore;
    private readonly ISessionFeedback _feedback;

    // Ids dos computadores controlados presentes. Id desconhecido (relay que não informa) é
    // contado em _anonymousFollowers para não perder a presença.
    private readonly HashSet<long> _followerIds = [];
    private int _anonymousFollowers;

    public RemoteSessionCoordinator(
        ITransportService transportService,
        ISpeechOutputService speechOutputService,
        IClipboardService clipboardService,
        IMotdSeenStore motdSeenStore,
        ISessionFeedback feedback)
    {
        _speechOutputService = speechOutputService;
        _clipboardService = clipboardService;
        _motdSeenStore = motdSeenStore;
        _feedback = feedback;

        transportService.MessageReceived += OnMessageReceived;
        transportService.StateChanged += OnTransportStateChanged;
    }

    public bool IsRemoteComputerPresent => _followerIds.Count > 0 || _anonymousFollowers > 0;

    public event EventHandler? PresenceChanged;

    private void OnTransportStateChanged(object? sender, ConnectionState state)
    {
        if (state == ConnectionState.Authenticated)
            return;

        // Fora de Authenticated ninguém mais está "presente", e o que ainda estiver na fila do
        // TTS é fala de uma sessão que acabou (ou caiu). Sem anúncio: a própria mudança de
        // status da conexão já é anunciada pela tela.
        _speechOutputService.Cancel();
        _speechOutputService.SetPaused(false);
        SetPresence(() =>
        {
            _followerIds.Clear();
            _anonymousFollowers = 0;
        }, announce: false);
    }

    private void OnMessageReceived(object? sender, IRemoteMessage message)
    {
        switch (message)
        {
            case ChannelJoinedMessage joined:
                HandleChannelJoined(joined);
                break;

            case ClientJoinedMessage joined:
                // Sem "client" (relay v1) não dá para saber o papel; num canal de controle remoto
                // quem entra depois do master é, na prática, o computador controlado.
                if (joined.Client is null || joined.Client.IsFollower)
                    SetPresence(() => AddFollower(joined.Client?.Id ?? joined.UserId), announce: true);
                break;

            case ClientLeftMessage left:
                SetPresence(() => RemoveFollower(left.Client?.Id ?? left.UserId), announce: true);
                break;

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
                _feedback.PlayTone(tone.Hz, tone.Length);
                break;

            case ErrorMessage error:
                _feedback.Announce(string.Format(AppResources.Session_RelayError, error.Message));
                break;
        }
    }

    private void HandleChannelJoined(ChannelJoinedMessage joined)
    {
        SetPresence(() =>
        {
            _followerIds.Clear();
            _anonymousFollowers = 0;

            if (joined.Clients.Count > 0)
            {
                foreach (var client in joined.Clients)
                {
                    if (client.IsFollower)
                        _followerIds.Add(client.Id);
                }
            }
            else
            {
                // Relay que só manda user_ids (sem papel de cada um): qualquer outro peer já
                // no canal conta como presente.
                foreach (var userId in joined.UserIds)
                    _followerIds.Add(userId);
            }
        }, announce: true);
    }

    private void AddFollower(long? id)
    {
        if (id is { } known)
            _followerIds.Add(known);
        else
            _anonymousFollowers++;
    }

    private void RemoveFollower(long? id)
    {
        if (id is { } known)
            _followerIds.Remove(known);
        else if (_anonymousFollowers > 0)
            _anonymousFollowers--;
    }

    private void SetPresence(Action mutate, bool announce)
    {
        var wasPresent = IsRemoteComputerPresent;
        mutate();
        var isPresent = IsRemoteComputerPresent;

        if (wasPresent == isPresent)
            return;

        PresenceChanged?.Invoke(this, EventArgs.Empty);

        if (announce)
            _feedback.Announce(isPresent ? AppResources.Session_RemoteJoined : AppResources.Session_RemoteLeft);
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
        _feedback.ShowMotd(motd.Motd);
    }
}
