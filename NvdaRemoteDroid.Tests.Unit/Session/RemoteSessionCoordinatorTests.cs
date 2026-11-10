using System.Globalization;
using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Protocol.SpeechSequence;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Session;
using NvdaRemoteDroid.Core.Speech;
using NvdaRemoteDroid.Core.Transport;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.Session;

[Collection(CultureCollection.Name)]
public class RemoteSessionCoordinatorTests
{
    private readonly ITransportService _transport = Substitute.For<ITransportService>();
    private readonly ISpeechOutputService _speech = Substitute.For<ISpeechOutputService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IMotdSeenStore _motdSeenStore = Substitute.For<IMotdSeenStore>();
    private readonly ISessionFeedback _feedback = Substitute.For<ISessionFeedback>();
    private readonly RemoteSessionCoordinator _sut;

    public RemoteSessionCoordinatorTests()
    {
        AppResources.Culture = new CultureInfo("pt-BR");
        _sut = new RemoteSessionCoordinator(_transport, _speech, _clipboard, _motdSeenStore, _feedback);
    }

    private void Receive(IRemoteMessage message) =>
        _transport.MessageReceived += Raise.Event<EventHandler<IRemoteMessage>>(_transport, message);

    private void ChangeState(ConnectionState state) =>
        _transport.StateChanged += Raise.Event<EventHandler<ConnectionState>>(_transport, state);

    private static ChannelJoinedMessage ChannelJoined(params ClientInfo[] clients) =>
        new("canal", clients.Select(c => c.Id).ToList(), clients);

    [Fact]
    public void Speak_IsForwardedToSpeechOutput_WithPriority_WithoutAnyScreenBeingOpen()
    {
        // Nenhuma ViewModel existe neste teste: a fala tem de sair só com o coordinator vivo.
        Receive(new SpeakMessage([new SpeechTextItem("olá mundo")], Priority: 2));

        _speech.Received(1).Speak("olá mundo", SpeechPriority.Now);
    }

    [Fact]
    public void Pause_IsForwardedToSpeechOutput()
    {
        Receive(new PauseSpeechMessage(true));

        _speech.Received(1).SetPaused(true);
    }

    [Fact]
    public void Cancel_StopsSpeech_AndClearsPause()
    {
        Receive(new CancelMessage());

        Received.InOrder(() =>
        {
            _speech.Cancel();
            _speech.SetPaused(false);
        });
    }

    [Fact]
    public void ShiftThenLetterOnNvda_PauseThenCancel_DoesNotLeaveSpeechMutedForever()
    {
        // Sequência real do NVDA follower para Shift+A: Shift sozinho → pause_speech(true); o "A"
        // → cancel (o NVDA zera a pausa sem mandar pause_speech(false)) → speak "A".
        var speech = new PausableSpeechOutput();
        _ = new RemoteSessionCoordinator(_transport, speech, _clipboard, _motdSeenStore, _feedback);

        Receive(new PauseSpeechMessage(true));
        Receive(new CancelMessage());
        Receive(new SpeakMessage([new SpeechTextItem("A")], Priority: 1));

        speech.Spoken.ShouldBe(["A"]);
    }

    /// <summary>Modela a pausa como o NativeSpeechOutputService: descarta fala enquanto pausado.</summary>
    private sealed class PausableSpeechOutput : ISpeechOutputService
    {
        private bool _paused;
        public List<string> Spoken { get; } = [];

        public void Speak(string utterance, SpeechPriority priority)
        {
            if (!_paused)
                Spoken.Add(utterance);
        }

        public void Cancel() { }

        public void SetPaused(bool paused) => _paused = paused;

        public IReadOnlyList<TtsEngineInfo> GetInstalledEngines() => [];

        public void Configure(string? enginePackage, float rate) { }

        public void SpeakSample(string text) { }
    }

    [Fact]
    public void ClipboardAndTone_AreForwarded()
    {
        Receive(new SetClipboardTextMessage("copiado"));
        Receive(new ToneMessage(440, 80, 50, 50));

        _clipboard.Received(1).SetTextAsync("copiado");
        _feedback.Received(1).PlayTone(440, 80);
    }

    [Fact]
    public void ChannelJoined_WithFollowerAlreadyInChannel_MarksRemoteComputerPresent()
    {
        var raised = 0;
        _sut.PresenceChanged += (_, _) => raised++;

        Receive(ChannelJoined(new ClientInfo(2, "slave")));

        _sut.IsRemoteComputerPresent.ShouldBeTrue();
        raised.ShouldBe(1);
        _feedback.Received(1).Announce("Computador remoto conectado");
    }

    [Fact]
    public void ChannelJoined_WithOnlyOtherMasters_IsNotPresent()
    {
        Receive(ChannelJoined(new ClientInfo(2, "master")));

        _sut.IsRemoteComputerPresent.ShouldBeFalse();
        _feedback.DidNotReceive().Announce(Arg.Any<string>());
    }

    [Fact]
    public void ChannelJoined_WithUserIdsButNoClients_TreatsPeersAsPresent()
    {
        // Relay que só informa user_ids (sem o papel de cada peer).
        Receive(new ChannelJoinedMessage("canal", [7], []));

        _sut.IsRemoteComputerPresent.ShouldBeTrue();
    }

    [Fact]
    public void ClientJoinedThenLeft_TogglesPresenceAndAnnouncesBoth()
    {
        Receive(ChannelJoined());
        _sut.IsRemoteComputerPresent.ShouldBeFalse();

        Receive(new ClientJoinedMessage(5, new ClientInfo(5, "slave")));
        _sut.IsRemoteComputerPresent.ShouldBeTrue();

        Receive(new ClientLeftMessage(5, new ClientInfo(5, "slave")));
        _sut.IsRemoteComputerPresent.ShouldBeFalse();

        Received.InOrder(() =>
        {
            _feedback.Announce("Computador remoto conectado");
            _feedback.Announce("Computador remoto desconectado");
        });
    }

    [Fact]
    public void AnotherMasterLeaving_DoesNotClearPresence()
    {
        Receive(ChannelJoined(new ClientInfo(2, "slave"), new ClientInfo(3, "master")));

        Receive(new ClientLeftMessage(3, new ClientInfo(3, "master")));

        _sut.IsRemoteComputerPresent.ShouldBeTrue();
    }

    [Fact]
    public void ClientJoined_WithoutRoleOrId_StillCountsAsPresent()
    {
        Receive(new ClientJoinedMessage(null));
        _sut.IsRemoteComputerPresent.ShouldBeTrue();

        Receive(new ClientLeftMessage(null));
        _sut.IsRemoteComputerPresent.ShouldBeFalse();
    }

    [Fact]
    public void LeavingAuthenticatedState_ClearsPresenceSilently_AndStopsSpeech()
    {
        Receive(ChannelJoined(new ClientInfo(2, "slave")));
        _feedback.ClearReceivedCalls();

        ChangeState(ConnectionState.Error);

        _sut.IsRemoteComputerPresent.ShouldBeFalse();
        _speech.Received(1).Cancel();
        _speech.Received(1).SetPaused(false);
        _feedback.DidNotReceive().Announce(Arg.Any<string>());
    }

    [Fact]
    public void Motd_IsShownOnce_UnlessForceDisplay()
    {
        _motdSeenStore.HasSeen("aviso").Returns(false, true, true);

        Receive(new MotdMessage("aviso"));
        Receive(new MotdMessage("aviso"));
        Receive(new MotdMessage("aviso", ForceDisplay: true));

        _feedback.Received(2).ShowMotd("aviso");
    }

    [Fact]
    public void RelayError_IsAnnounced()
    {
        Receive(new ErrorMessage("algo deu errado"));

        _feedback.Received(1).Announce("Erro do servidor: algo deu errado");
    }
}
