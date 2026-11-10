using System.Globalization;
using NSubstitute;
using NvdaRemoteDroid.Core.Gateway;
using NvdaRemoteDroid.Core.Keyboard;
using NvdaRemoteDroid.Core.Protocol.Messages;
using NvdaRemoteDroid.Core.Resources.Strings;
using NvdaRemoteDroid.Core.Session;
using NvdaRemoteDroid.Core.Transport;
using NvdaRemoteDroid.Core.ViewModels;
using Shouldly;

namespace NvdaRemoteDroid.Tests.Unit.ViewModels;

[Collection(CultureCollection.Name)]
public class RemoteControlViewModelBaseTests
{
    private readonly ITransportService _transport = Substitute.For<ITransportService>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IRemoteSessionCoordinator _coordinator = Substitute.For<IRemoteSessionCoordinator>();
    private readonly List<IRemoteMessage> _sent = [];

    public RemoteControlViewModelBaseTests()
    {
        AppResources.Culture = new CultureInfo("pt-BR");
        _transport.SendAsync(Arg.Do<IRemoteMessage>(_sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    private TestRemoteControlViewModel CreateSut(ConnectionState state = ConnectionState.Authenticated, bool remotePresent = true)
    {
        _transport.State.Returns(state);
        _coordinator.IsRemoteComputerPresent.Returns(remotePresent);
        return new TestRemoteControlViewModel(_transport, _clipboard, _coordinator);
    }

    [Fact]
    public void Constructor_SeedsIsConnected_FromTransportStateAlreadyAuthenticated()
    {
        // Reproduz a regressão: o VM (transient) só é criado DEPOIS que o transporte (singleton)
        // já reportou Authenticated — o evento StateChanged one-shot já foi perdido, então
        // IsConnected precisa nascer certo a partir do estado atual, não só de eventos futuros.
        var sut = CreateSut(ConnectionState.Authenticated);

        sut.IsConnected.ShouldBeTrue();
        sut.StatusText.ShouldBe("Conectado");
    }

    [Fact]
    public void Constructor_SeedsIsConnected_FalseWhenTransportNotAuthenticated()
    {
        var sut = CreateSut(ConnectionState.Disconnected);

        sut.IsConnected.ShouldBeFalse();
        sut.SendSasCommand.CanExecute(null).ShouldBeFalse();
        sut.DisconnectCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void StatusText_SaysWaitingForRemoteComputer_UntilItJoinsTheChannel()
    {
        var sut = CreateSut(ConnectionState.Authenticated, remotePresent: false);
        sut.StatusText.ShouldBe("Conectado, aguardando o computador remoto entrar no canal");

        _coordinator.IsRemoteComputerPresent.Returns(true);
        _coordinator.PresenceChanged += Raise.Event<EventHandler>(_coordinator, EventArgs.Empty);

        sut.StatusText.ShouldBe("Conectado");
    }

    [Fact]
    public void StateChanged_UpdatesIsConnectedAndCommandAvailability()
    {
        var sut = CreateSut(ConnectionState.Disconnected);

        _transport.StateChanged += Raise.Event<EventHandler<ConnectionState>>(_transport, ConnectionState.Authenticated);

        sut.IsConnected.ShouldBeTrue();
        sut.SendSasCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task SendChord_PressesModifiersInOrder_TapsKey_ReleasesInReverse()
    {
        var sut = CreateSut();
        var ctrl = VirtualKeyboardLayout.Ctrl;
        var win = VirtualKeyboardLayout.Win;

        await sut.SendChordAsync([ctrl, win], new VirtualKeyMapping(VirtualKeyCode.D, false));

        _sent.Cast<KeyMessage>().Select(k => (k.VkCode, k.Extended, k.Pressed)).ShouldBe(
        [
            ((int)VirtualKeyCode.Control, false, true),
            ((int)VirtualKeyCode.LeftWindows, true, true),
            ((int)VirtualKeyCode.D, false, true),
            ((int)VirtualKeyCode.D, false, false),
            ((int)VirtualKeyCode.LeftWindows, true, false),
            ((int)VirtualKeyCode.Control, false, false),
        ]);
        _sent.Cast<KeyMessage>().ShouldAllBe(k => k.ScanCode == null);
    }

    [Fact]
    public async Task SendText_SetsRemoteClipboardThenPastesWithCtrlV_AndClearsTheField()
    {
        var sut = CreateSut();
        sut.TextToSend = "ação & coração";

        await sut.SendTextCommand.ExecuteAsync(null);

        _sent[0].ShouldBeOfType<SetClipboardTextMessage>().Text.ShouldBe("ação & coração");
        _sent.Skip(1).Cast<KeyMessage>().Select(k => (k.VkCode, k.Pressed)).ShouldBe(
        [
            ((int)VirtualKeyCode.Control, true),
            ((int)VirtualKeyCode.V, true),
            ((int)VirtualKeyCode.V, false),
            ((int)VirtualKeyCode.Control, false),
        ]);
        sut.TextToSend.ShouldBeEmpty();
        sut.Announced.ShouldBe(["Texto enviado"]);
    }

    [Fact]
    public void SendText_CannotExecute_WhenEmptyOrDisconnected()
    {
        var sut = CreateSut();
        sut.SendTextCommand.CanExecute(null).ShouldBeFalse();

        sut.TextToSend = "abc";
        sut.SendTextCommand.CanExecute(null).ShouldBeTrue();

        _transport.StateChanged += Raise.Event<EventHandler<ConnectionState>>(_transport, ConnectionState.Error);
        sut.SendTextCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void GoToConnection_DelegatesToPlatformNavigation()
    {
        var sut = CreateSut(ConnectionState.Disconnected);

        sut.GoToConnectionCommand.Execute(null);

        sut.NavigatedToConnection.ShouldBeTrue();
    }

    private sealed class TestRemoteControlViewModel(
        ITransportService transportService,
        IClipboardService clipboardService,
        IRemoteSessionCoordinator sessionCoordinator)
        : RemoteControlViewModelBase(transportService, clipboardService, sessionCoordinator)
    {
        public List<string> Announced { get; } = [];
        public bool NavigatedToConnection { get; private set; }

        protected override void AnnounceStatus(string text) => Announced.Add(text);
        protected override void NavigateToConnection() => NavigatedToConnection = true;
    }
}
